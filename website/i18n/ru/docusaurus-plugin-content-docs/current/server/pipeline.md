---
sidebar_position: 4
title: Фильтры и хуки
---

# Фильтры и хуки

В DarkWS есть четыре точки расширения вокруг запросов и подключений:

| Расширение | Когда выполняется | Типичное применение |
| --- | --- | --- |
| Фильтр запросов | Вокруг каждого корректно сформированного запроса, включая отклонённые | Метрики, трассировка, логирование |
| Инициализатор области | Перед каждым вызовом обработчика | Подготовка scoped-сервисов (тенант, текущий пользователь) |
| Фильтр действий | Вокруг привязанного обработчика | Проверка прав, валидация, преобразование ошибок |
| Хуки подключения | При открытии, смене аутентификации и закрытии | Присутствие (presence), очистка ресурсов |

Фильтры и инициализаторы разрешаются из области сообщения, поэтому в них можно
внедрять scoped-сервисы.

## Порядок обработки запроса {#order-of-a-request}

1. Сообщение разбирается. Ответы на некорректный JSON и отказы из-за занятости
   отправляются на этом этапе, до создания какой-либо области.
2. Создаётся область сообщения, и запускаются **фильтры запросов**, начиная с внешнего.
3. Выполняется поиск действия и проверка авторизации; payload привязывается к параметру.
   Ошибки на этом этапе дают `ErrorResponse`, который видят фильтры запросов.
4. **Инициализаторы области** выполняются в порядке регистрации.
5. **Фильтры действий** выполняются вокруг обработчика, начиная с внешнего.
6. Выполняется обработчик, и записывается ответ.

## Фильтры запросов {#request-filters}

```csharp
public sealed class MetricsFilter(ILogger<MetricsFilter> logger) : IDarkWsRequestFilter {
    public async ValueTask<IResponse> InvokeAsync(
        DarkWsRequestContext context, Func<ValueTask<IResponse>> next) {
        var started = Stopwatch.GetTimestamp();
        try {
            var response = await next();
            var outcome = response is ErrorResponse error ? error.Error : "ok";
            logger.LogInformation("{Action} {Outcome} in {Elapsed}",
                context.ActionName, outcome, Stopwatch.GetElapsedTime(started));
            return response;
        } catch (Exception exception) {
            logger.LogWarning(exception, "{Action} failed", context.ActionName);
            throw;
        }
    }
}

builder.Services.AddDarkWs().AddRequestFilter<MetricsFilter>();
```

Фильтры запросов видят:

- неизвестные действия (`context.Action` равен null), отсутствие авторизации и
  некорректный payload — как `ErrorResponse`, у которого `Error` содержит настроенный код;
- сбой инициализатора области, фильтра действий или обработчика — как исключение. Затем
  DarkWS отвечает кодом `RequestFailedError` или ответом из `DarkWsException`.

`DarkWsRequestContext` содержит `RequestId`, клиентское `ActionName`, зарегистрированное
`Action` (или null), `RawPayload`, `Session`, `Services` и `CancellationToken`.

## Инициализаторы области {#scope-initializers}

Инициализатор области подготавливает scoped-сервисы до создания обработчика:

```csharp
public sealed class TenantInitializer : IDarkWsScopeInitializer {
    public ValueTask InitializeAsync(
        IServiceProvider scopedServices, IDarkWsContextAccessor context, CancellationToken ct) {
        if (context.Session is AppSession session) {
            scopedServices.GetRequiredService<TenantContext>().AccountId = session.AccountId;
        }
        return ValueTask.CompletedTask;
    }
}

builder.Services.AddScoped<TenantContext>();
builder.Services.AddDarkWs().AddScopeInitializer<TenantInitializer>();
```

`context.Action` описывает вызываемое действие.

## Фильтры действий {#action-filters}

Фильтры действий оборачивают обработчик после поиска действия, авторизации, привязки и
инициализаторов. Они выполняются в порядке регистрации, первый — самый внешний:

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireRoleAttribute(string role) : Attribute {
    public string Role { get; } = role;
}

public sealed class RoleFilter : IDarkWsActionFilter {
    public ValueTask<IResponse> InvokeAsync(
        DarkWsActionContext context, Func<ValueTask<IResponse>> next) {
        var required = context.Action.Attributes
            .Concat(context.Action.HandlerAttributes)
            .OfType<RequireRoleAttribute>();
        foreach (var attribute in required) {
            if (context.Session?.User.IsInRole(attribute.Role) != true) {
                return ValueTask.FromResult<IResponse>(new ErrorResponse("auth:forbidden"));
            }
        }
        return next();
    }
}

builder.Services.AddDarkWs().AddActionFilter<RoleFilter>();
```

Фильтр может вернуть собственный `IResponse`, не вызывая `next`, либо проверить
ответ или перехватить исключение из `next`. Вызывайте `next` не более одного раза.

`DarkWsActionContext` предоставляет:

| Свойство | Значение |
| --- | --- |
| `Action` | `DarkWsActionInfo`: `Name`, `HandlerType`, `Method`, `Attributes` метода, `HandlerAttributes` класса |
| `RequestId` | Идентификатор запроса клиента |
| `Payload` / `RawPayload` | Десериализованный параметр и полученный JSON |
| `Session` | Сессия, зафиксированная для этого запроса |
| `Services` | Провайдер сервисов области сообщения |
| `CancellationToken` | Отменяется при остановке подключения |

Пользовательские атрибуты, как `RequireRole` выше, — поддерживаемый способ добавить
декларативную авторизацию, поскольку `[Authorize]` приводит к ошибке регистрации.

## Доступ к контексту {#context-accessor}

Scoped-сервисы могут внедрить `IDarkWsContextAccessor`, чтобы читать текущий запрос:

| Свойство | Значение |
| --- | --- |
| `Connection` | Текущее подключение |
| `Session` | Сессия, с которой было авторизовано действие |
| `HttpContext` | Контекст запроса на upgrade |
| `AspNetSession` | ASP.NET `ISession` или null |
| `ConnectionAborted` | Отменяется при остановке подключения |
| `Action` | Вызываемое действие; null в хуках подключения |

Accessor инициализируется только внутри области сообщения. Чтение из обычного
HTTP-запроса или из области подключения выбрасывает `InvalidOperationException`.

## Хуки подключения {#connection-hooks}

Унаследуйтесь от `DarkWsConnectionHooks` и переопределите нужные методы:

```csharp
public sealed class PresenceHooks(IBroadcaster broadcaster) : DarkWsConnectionHooks {
    public override Task OnOpenAsync(IDarkWsContextAccessor context) =>
        context.Session is AppSession session
            ? broadcaster.PublishAsync(BroadcastTarget.Group($"account:{session.AccountId}"),
                "presence:online", new { session.UserId })
            : Task.CompletedTask;

    public override Task OnAuthenticatedAsync(IDarkWsContextAccessor context, IDarkWsSession? previousSession) =>
        Task.CompletedTask;

    public override Task OnCloseAsync(IDarkWsContextAccessor context) =>
        Task.CompletedTask;
}

builder.Services.AddDarkWs().AddConnectionHooks<PresenceHooks>();
```

| Хук | Когда вызывается |
| --- | --- |
| `OnOpenAsync` | После регистрации подключения |
| `OnAuthenticatedAsync` | После того как `auth:` заменяет сессию, `logout` очищает её или неудачный `auth:` очищает её; `previousSession` — прежняя сессия, а текущая может быть null |
| `OnCloseAsync` | После удаления подключения из хранилища |

Правила для хуков:

- Используйте аргумент `context`. Внедрённый `IDarkWsContextAccessor` в этой области
  не инициализирован.
- Хуки живут в области запроса на upgrade на протяжении всего подключения. Короткоживущие
  зависимости получайте через `IServiceScopeFactory`.
- В `OnCloseAsync` `context.ConnectionAborted` — это дедлайн `ShutdownTimeout`,
  общий с close handshake, поэтому асинхронная очистка может выполняться до его истечения.
