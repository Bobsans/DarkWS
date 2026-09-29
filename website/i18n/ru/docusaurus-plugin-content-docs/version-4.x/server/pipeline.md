---
sidebar_position: 4
title: Инициализаторы области и промежуточное ПО
---

# Инициализаторы области и промежуточное ПО

В DarkWS есть две точки расширения вокруг запросов и подключений:

| Расширение | Когда выполняется | Типичное применение |
| --- | --- | --- |
| Инициализатор области | Перед каждым вызовом обработчика | Подготовка scoped-сервисов (тенант, текущий пользователь) |
| Промежуточное ПО (middleware) | При открытии, смене аутентификации и закрытии | Присутствие (presence), очистка ресурсов |

Инициализаторы области разрешаются из области сообщения, поэтому в них можно
внедрять scoped-сервисы.

## Порядок обработки запроса {#order-of-a-request}

1. Сообщение разбирается. На некорректный JSON отправляется `InvalidRequestError`,
   если удаётся прочитать идентификатор запроса.
2. Выполняется поиск действия и проверка авторизации; payload десериализуется.
   На ошибки на этом этапе отправляется `InvalidActionError`, `AuthorizationRequiredError`
   или `InvalidRequestError`.
3. Создаётся область сообщения, и **инициализаторы области** выполняются в порядке регистрации.
4. Обработчик разрешается из области и выполняется, после чего записывается ответ.

На исключение из инициализатора области или обработчика отправляется `RequestFailedError`
или ответ из `DarkWsException`.

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

`CancellationToken` отменяется при остановке подключения.

## Доступ к контексту {#context-accessor}

Scoped-сервисы могут внедрить `IDarkWsContextAccessor`, чтобы читать текущий запрос:

| Свойство | Значение |
| --- | --- |
| `Connection` | Текущее подключение |
| `Session` | Текущая сессия подключения или null |
| `HttpContext` | Контекст запроса на upgrade |
| `AspNetSession` | ASP.NET `ISession` или null |
| `ConnectionAborted` | Отменяется при остановке подключения |

`Session` читает сессию подключения в момент обращения, поэтому `auth:` или `logout`,
обработанные во время выполнения действия, меняют возвращаемое значение.

Accessor инициализируется только внутри области сообщения. Чтение из обычного
HTTP-запроса или из области подключения выбрасывает `InvalidOperationException`.

## Промежуточное ПО {#middleware}

Унаследуйтесь от `DarkWsMiddleware` и переопределите нужные методы:

```csharp
public sealed class PresenceMiddleware(IBroadcaster broadcaster) : DarkWsMiddleware {
    public override Task OnOpenAsync(IDarkWsContextAccessor context) =>
        context.Session is AppSession session
            ? broadcaster.BroadcastToGroupAsync($"account:{session.AccountId}",
                "presence:online", new { session.UserId })
            : Task.CompletedTask;

    public override Task OnAuthenticatedAsync(IDarkWsContextAccessor context, IDarkWsSession? previousSession) =>
        Task.CompletedTask;

    public override Task OnCloseAsync(IDarkWsContextAccessor context) =>
        Task.CompletedTask;
}

builder.Services.AddDarkWs().AddHandlersFromAssemblyContaining<Program>();
```

Отдельного метода регистрации нет: `AddHandlersFromAssembly` и
`AddHandlersFromAssemblyContaining<T>` регистрируют каждый конкретный подкласс
`DarkWsMiddleware` из сканируемой сборки вместе с обработчиками. Хуки всех middleware
выполняются друг за другом.

| Хук | Когда вызывается |
| --- | --- |
| `OnOpenAsync` | После регистрации подключения |
| `OnAuthenticatedAsync` | После того как `auth:` заменяет сессию, `logout` очищает её или неудачный `auth:` очищает её; `previousSession` — прежняя сессия, а текущая может быть null |
| `OnCloseAsync` | После удаления подключения из хранилища и после того, как незавершённые обработчики закончили работу или истёк `ShutdownTimeout` |

Правила для middleware:

- Используйте аргумент `context`. Внедрённый `IDarkWsContextAccessor` в этой области
  не инициализирован.
- Middleware живёт в области запроса на upgrade на протяжении всего подключения.
  Короткоживущие зависимости получайте через `IServiceScopeFactory`.
- Исключение из `OnOpenAsync` или `OnAuthenticatedAsync` завершает подключение.
  Исключение из `OnCloseAsync` записывается в лог.
- В `OnCloseAsync` `context.ConnectionAborted` уже отменён. DarkWS ждёт хук только до
  дедлайна `ShutdownTimeout`, общего с незавершёнными обработчиками и close handshake.
