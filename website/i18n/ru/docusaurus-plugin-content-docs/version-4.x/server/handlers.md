---
sidebar_position: 1
title: Обработчики и действия
---

# Обработчики и действия

Обработчик — это класс, помеченный `[Handler(name)]` и унаследованный от `HandlerBase`.
Его публичные методы с атрибутом `[Action(name)]` являются действиями. Клиент вызывает
действие по составному имени `handler:action`.

```csharp
using DarkWS;

[Handler("message")]
public sealed class MessageHandler(MessageService messages) : HandlerBase {
    [Action("get")]
    public async Task<IResponse> GetAsync(Guid id) {
        var message = await messages.FindAsync(id, ConnectionAborted);
        return message is null ? Error("message:not-found") : Ok(message);
    }

    [Action("count")]
    public IResponse Count() => Ok(messages.Count);
}
```

## Регистрация {#registration}

Регистрируйте обработчики при добавлении DarkWS:

```csharp
builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<Program>()
    .AddHandlersFromAssembly(typeof(ExternalHandler).Assembly);
```

При регистрации сборка сканируется на неабстрактные классы, унаследованные от `HandlerBase`,
и каждое действие проверяется. Некорректные объявления приводят к ошибке при запуске:
`InvalidOperationException` с указанием типа, метода и причины.

- Вызывайте `AddDarkWs()` один раз на коллекцию сервисов; повторный вызов выбрасывает
  исключение. Используйте возвращённый `DarkWsBuilder` для добавления других сборок,
  аутентификатора и инициализаторов областей.
- Два действия с одинаковым составным именем приводят к ошибке регистрации. Обработчик без
  `[Handler]` публикует свои действия под одним лишь именем действия.
- Действия должны быть публичными методами экземпляра, объявленными в сканируемом классе.
  Унаследованные методы не сканируются: объявите или переопределите действие в конкретном
  обработчике и пометьте его `[Action]`.
- Действие возвращает строго `IResponse` или `Task<IResponse>`.
- Действие принимает ноль или один параметр payload. Обобщённые методы, а также параметры,
  передаваемые по ссылке, byref-like-параметры и указатели отклоняются.
- Сканирование сборки также регистрирует каждый неабстрактный наследник `DarkWsMiddleware`
  (см. [Middleware](pipeline.md#middleware)). Инициализаторы областей регистрируются
  явно через `AddScopeInitializer<T>()`.

## Зависимости и область {#dependencies-and-scope}

Каждый запрос выполняется в новой асинхронной области DI. Обработчик создаётся из этой
области, поэтому внедрение через конструктор scoped-сервисов, например `DbContext`, безопасно.
Область освобождается, когда действие возвращает результат, — до записи ответа.

Внутри действия `HandlerBase` предоставляет контекст запроса:

| Член | Значение |
| --- | --- |
| `Connection` | Текущее `IWebSocketConnection` (`Id`, `Session`, `IsOpen`, `HttpContext`, `SendAsync`, `CloseAsync`) |
| `Session` | Текущая сессия соединения; для анонимного соединения выбрасывает исключение |
| `HttpContext` | `HttpContext` upgrade-запроса, общий для всего соединения |
| `AspNetSession` | ASP.NET `ISession`, если установлен middleware сессий, иначе null |
| `ConnectionAborted` | Отменяется при остановке соединения; передавайте его в асинхронную работу |

`HandlerBase` также предоставляет методы рассылки `BroadcastAsync`,
`BroadcastToSessionAsync`, `BroadcastToGroupAsync` и `BroadcastToSelfAsync` для
вызывающего соединения; см. [Рассылки](broadcasts.md).

Наследуйтесь от `HandlerBase<TSession>`, чтобы получить типизированный `Session`; см.
[Аутентификация и сессии](authentication.md).

Сервисы вне обработчиков могут внедрить `IDarkWsContextAccessor` для доступа к тем же данным;
см. [Инициализаторы областей и middleware](pipeline.md#context-accessor).

## Payload {#payloads}

Поле `data` запроса десериализуется в параметр действия с помощью
`DarkWsOptions.JsonOptions` (веб-умолчания: camelCase, без учёта регистра).

- Nullable-параметр (`string?`, `Input?`, `int?`) принимает отсутствующий или null payload.
- Non-nullable-параметр требует payload. Если данные опущены или равны null, возвращается
  `darkws:error:invalid-request`, а обработчик не вызывается.
- Несоответствие типов и числовое переполнение в `data` возвращают
  `darkws:error:invalid-request` ещё до создания обработчика.
- На сообщение, которое не является корректным JSON или не содержит `id` или `action`,
  отвечается `darkws:error:invalid-request`, только если удаётся прочитать его `id`; иначе
  ответа нет.
- Используйте `JsonElement`, чтобы принимать произвольный JSON.

## Результаты {#results}

Возвращайте один из хелперов:

| Хелпер | Ответ в протоколе |
| --- | --- |
| `Ok()` | `{ "id": "…" }` |
| `Ok(value)` | `{ "id": "…", "data": value }` |
| `Error("code")` | `{ "id": "…", "error": "code" }` |
| `Error("code", details)` | `{ "id": "…", "error": "code", "data": details }` |

Коды ошибок не должны быть пустыми. Используйте стабильные коды с пространством имён,
например `message:not-found`, по которым клиенты смогут ветвиться.

Результаты сериализуются после освобождения области сообщения. Материализуйте данные,
зависящие от scoped-сервисов (например, вызовите `ToListAsync()` у запроса), до того как
вернуть их. Результат `null`, значение, которое не удаётся сериализовать, или упавший
пользовательский `IResponse` логируются как предупреждение, и клиент не получает ответа
на этот запрос. Соединение остаётся открытым.

Чтобы написать собственный ответ, реализуйте `IResponse.WriteResultAsync(ResponseContext, CancellationToken)`
и отправляйте данные через `context.SendAsync(data)`.

## Ошибки {#errors}

Выбросьте `ErrorResponseException` из любого места цепочки вызовов, чтобы вернуть
контролируемую ошибку:

```csharp
if (!await permissions.CanEditAsync(Session.UserId, id)) {
    throw new ErrorResponseException("message:forbidden");
}
throw new ErrorResponseException<ValidationDetails>("message:invalid", details);
```

Любое другое исключение логируется как предупреждение, и на него отвечается
`darkws:error:request-failed`. Его сообщение никогда не отправляется клиенту. Чтобы создать
собственный тип исключения с ответом протокола, унаследуйтесь от `DarkWsException` и
реализуйте `GetResponse()`.

Встроенные коды ошибок перечислены в разделе [Протокол](../protocol.md).

## Авторизация {#authorization}

По умолчанию обработчики требуют аутентифицированной сессии. Пометьте обработчик или
отдельное действие `[AllowAnonymous]`, чтобы сделать его публичным:

```csharp
[Handler("system"), AllowAnonymous]
public sealed class SystemHandler : HandlerBase {
    [Action("ping")]
    public IResponse Ping() => Ok(new { ServerTime = DateTimeOffset.UtcNow });
}
```

Вызов защищённого действия без сессии или с сессией, чей `User` не аутентифицирован,
возвращает `darkws:error:authorization-required`.

Встроенная авторизация различает только аутентифицированные и анонимные соединения.
`[Authorize]`, атрибуты ролей и политик и любые другие `IAuthorizeData` на обработчике
или действии приводят к ошибке регистрации, а не игнорируются молча. Проверяйте прикладные
разрешения в действии и возвращайте контролируемую ошибку при отказе.

## Параллелизм {#concurrency}

Одновременно выполняется до `MaxConcurrentRequestsPerConnection` (16) действий одного
соединения, и ответы могут приходить не по порядку. Когда лимит достигнут, DarkWS
перестаёт читать сокет, пока не завершится какое-либо действие, поэтому `ping`, `auth:` и
`logout` тоже ждут. Параллельные действия разделяют `HttpContext`, `Items`, features и
`AspNetSession` соединения, которые не являются потокобезопасными:

- Храните состояние отдельного запроса в scoped-сервисах, а не в `HttpContext.Items`.
- Считывайте нужные действию данные из `HttpContext` в самом его начале.
- Сериализуйте запись в `ISession` самостоятельно или установите `MaxConcurrentRequestsPerConnection = 1`.

Действие запускается в цикле диспетчеризации соединения, поэтому синхронный обработчик
или часть асинхронного обработчика до первого `await` задерживает диспетчеризацию
следующего сообщения.

## Состояние сессии ASP.NET {#aspnet-session-state}

Чтобы использовать `AspNetSession`, зарегистрируйте `AddSession()` и вызовите `UseSession()` до
`MapDarkWs()`. WebSocket — это один длинный запрос, поэтому вызывайте
`HttpContext.Session.CommitAsync()`, когда изменение нужно сохранить немедленно.

## Обработчики, переживающие соединение {#handlers-that-outlive-the-connection}

При завершении работы DarkWS отменяет `ConnectionAborted` и ждёт до `ShutdownTimeout`.
.NET не может остановить код, игнорирующий отмену: такой обработчик удерживает свою область,
пока не завершится, а его ответ отбрасывается. После завершения работы upgrade-запрос
закончен, и ASP.NET Core может переиспользовать его `HttpContext`, поэтому не читайте
`HttpContext`, `AspNetSession` и `Connection.HttpContext` из такого обработчика. Скопируйте
нужные значения запроса до начала долгой работы; `Session` и его `User` остаются доступными
для чтения.
