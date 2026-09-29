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

При регистрации сборка сканируется на классы с `[Handler]`, и каждое действие проверяется.
Некорректные объявления приводят к ошибке при запуске: `InvalidOperationException`
с указанием типа, метода и причины.

- Вызывайте `AddDarkWs()` один раз на коллекцию сервисов; повторный вызов выбрасывает
  исключение. Используйте возвращённый `DarkWsBuilder` для добавления других сборок,
  фильтров и хуков.
- Имена обработчиков и действий не должны быть пустыми и не должны иметь пробелов по краям.
- Действия должны быть публичными методами экземпляра, объявленными в сканируемом классе.
  Унаследованные методы не сканируются: объявите или переопределите действие в конкретном
  обработчике и пометьте его `[Action]`.
- Действие возвращает строго `IResponse` или `Task<IResponse>`.
- Действие принимает ноль или один параметр payload. Обобщённые методы, а также параметры,
  передаваемые по ссылке, byref-like-параметры и указатели отклоняются.
- Сканирование сборки регистрирует только обработчики. Фильтры, инициализаторы и хуки
  регистрируются явно.

## Зависимости и область {#dependencies-and-scope}

Каждый запрос выполняется в новой асинхронной области DI. Обработчик создаётся из этой
области, поэтому внедрение через конструктор scoped-сервисов, например `DbContext`, безопасно.
Область освобождается после записи ответа.

Внутри действия `HandlerBase` предоставляет контекст запроса:

| Член | Значение |
| --- | --- |
| `Connection` | Текущее `IWebSocketConnection` (`Id`, `Session`, `IsOpen`, `CloseAsync`, `Abort`) |
| `Session` | Аутентифицированная сессия; для анонимного соединения выбрасывает исключение |
| `HttpContext` | `HttpContext` upgrade-запроса, общий для всего соединения |
| `AspNetSession` | ASP.NET `ISession`, если установлен middleware сессий, иначе null |
| `ConnectionAborted` | Отменяется при остановке соединения; передавайте его в асинхронную работу |
| `Services` | Провайдер сервисов области сообщения |
| `Self` | Цель рассылки для вызывающего соединения |

Наследуйтесь от `HandlerBase<TSession>`, чтобы получить типизированный `Session`; см.
[Аутентификация и сессии](authentication.md).

Сервисы вне обработчиков могут внедрить `IDarkWsContextAccessor` для доступа к тем же данным;
см. [Фильтры и хуки](pipeline.md#context-accessor).

## Payload {#payloads}

Поле `data` запроса десериализуется в параметр действия с помощью
`DarkWsOptions.JsonOptions` (веб-умолчания: camelCase, без учёта регистра).

- Nullable-параметр (`string?`, `Input?`, `int?`) принимает отсутствующий или null payload.
- Non-nullable-параметр требует payload. Если данные опущены или равны null, возвращается
  `darkws:error:invalid-request`, а обработчик не вызывается.
- `AllowNullPayloads = true` передаёт опущенные или null-данные в ссылочные параметры,
  даже если они аннотированы как non-nullable; тогда обработчик сам должен проверять null.
  Non-nullable значимые типы по-прежнему не могут получить null.
- Некорректный JSON, несоответствие типов и числовое переполнение возвращают
  `darkws:error:invalid-request` ещё до создания обработчика.
- Используйте `JsonElement`, чтобы принимать произвольный JSON.

## Результаты {#results}

Возвращайте один из хелперов:

| Хелпер | Ответ в протоколе |
| --- | --- |
| `Ok()` | `{ "id": "…" }` |
| `Ok(value)` | `{ "id": "…", "data": value }`; `data` записывается, даже если `value` равно null |
| `Error("code")` | `{ "id": "…", "error": "code" }` |
| `Error("code", details)` | `{ "id": "…", "error": "code", "data": details }` |

Коды ошибок не должны быть пустыми. Используйте стабильные коды с пространством имён,
например `message:not-found`, по которым клиенты смогут ветвиться.

Результаты сериализуются до освобождения области сообщения, поэтому отложенный запрос
к scoped-сервису всё ещё перечисляется, пока этот сервис жив. На результат `null`, значение,
которое не удаётся сериализовать (циклическая ссылка или неподдерживаемый тип), или
пользовательский `IResponse`, упавший до отправки, возвращается `darkws:error:request-failed`,
и ошибка логируется. Соединение остаётся открытым.

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

Встроенные коды ошибок перечислены в разделе [Протокол](../protocol.md#error-codes).

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

Вызов защищённого действия без сессии возвращает
`darkws:error:authorization-required`.

Встроенная авторизация различает только аутентифицированные и анонимные соединения.
`[Authorize]`, атрибуты ролей и политик и любые другие `IAuthorizeData` на обработчике
или действии приводят к ошибке регистрации, а не игнорируются молча. Проверяйте прикладные
разрешения в действии или в [фильтре действий](pipeline.md#action-filters) и
возвращайте контролируемую ошибку при отказе.

## Параллелизм {#concurrency}

Одновременно выполняется до `MaxConcurrentRequestsPerConnection` (16) действий одного
соединения, и ответы могут приходить не по порядку. Параллельные действия разделяют
`HttpContext`, `Items`, features и `AspNetSession` соединения, которые не являются
потокобезопасными:

- Храните состояние отдельного запроса в scoped-сервисах, а не в `HttpContext.Items`.
- Считывайте нужные действию данные из `HttpContext` в самом его начале.
- Сериализуйте запись в `ISession` самостоятельно или установите `MaxConcurrentRequestsPerConnection = 1`.

По умолчанию действие запускается в цикле диспетчеризации соединения, поэтому синхронный
обработчик или часть асинхронного обработчика до первого `await` задерживает диспетчеризацию
следующего сообщения. Установите `RunActionsOnThreadPool = true`, чтобы вместо этого
планировать действия в пуле потоков. Ограничение параллелизма и порядок команд при этом
сохраняются.

## Состояние сессии ASP.NET {#aspnet-session-state}

Чтобы использовать `AspNetSession`, зарегистрируйте `AddSession()` и вызовите `UseSession()` до
`MapDarkWs()`. WebSocket — это один длинный запрос, поэтому вызывайте
`HttpContext.Session.CommitAsync()`, когда изменение нужно сохранить немедленно.

## Обработчики, переживающие соединение {#handlers-that-outlive-the-connection}

При завершении работы DarkWS отменяет `ConnectionAborted` и ждёт до `ShutdownTimeout`.
.NET не может остановить код, игнорирующий отмену: такой обработчик удерживает свою область,
пока не завершится, а его ответ отбрасывается. После завершения работы `HttpContext`
upgrade-запроса переиспользуется, поэтому `HttpContext`, `AspNetSession` и `Connection.HttpContext`
выбрасывают `ObjectDisposedException`. Скопируйте нужные значения запроса до начала долгой работы;
`Session` и его `User` остаются доступными для чтения.
