---
sidebar_position: 2
title: .NET-клиент
---

# .NET-клиент

`DarkWS.Client` — асинхронный клиент для .NET 8, 9 и 10. У него нет зависимостей от
пакетов во время выполнения и не требуется ASP.NET, поэтому он работает в консольных приложениях, сервисах,
десктопных приложениях и интеграционных тестах.

```bash
dotnet add package DarkWS.Client
```

```csharp
using DarkWS.Client;

await using var client = new DarkWsClient(new Uri("wss://example.com/ws"));
var result = await client.RequestAsync<SumResult>("math:sum", new { left = 10, right = 20 });
Console.WriteLine(result.Value); // 30

public sealed record SumResult(int Value);
```

Первый запрос открывает соединение. Вызовите `ConnectAsync(ct)` для ранней
проверки готовности или если приложение только слушает рассылки.

Для полной настройки передайте `DarkWsClientOptions`:

```csharp
await using var client = new DarkWsClient(new DarkWsClientOptions {
    Endpoint = new Uri("wss://example.com/ws"),
    RequestTimeout = TimeSpan.FromSeconds(30),
    AuthenticationTokenProvider = ct => tokenStore.GetAccessTokenAsync(ct),
});
```

## Запросы {#requests}

| Вызов | Отправляет | Возвращает |
| --- | --- | --- |
| `RequestAsync<T>(action, ct)` | Без `data` | Десериализованное `data` |
| `RequestAsync<T>(action, payload, ct)` | `payload` как `data` | Десериализованное `data` |
| `RequestAsync(action, ct)` | Без `data` | Завершается по подтверждению |
| `RequestAsync(action, payload, ct)` | `payload` как `data` | Завершается по подтверждению |

- Действия без результата всё равно ждут подтверждения и выбрасывают исключение при ошибках сервера.
- `(object?)null` отправляет явный JSON null; если payload опущен, `data` тоже опускается.
- Типизированные запросы требуют поле `data` в ответе.
- Nullable-аннотации во время выполнения не проверяются; JSON null обрабатывается по правилам
  System.Text.Json.
- Для динамических результатов используйте `JsonElement`. Возвращённые элементы остаются валидными после
  освобождения буферов приёма.

Ошибка сервера выбрасывает `DarkWsResponseException`:

```csharp
try {
    await client.RequestAsync("message:delete", new { id }, ct);
} catch (DarkWsResponseException error) when (error.Code == "message:forbidden") {
    ShowForbidden();
}
```

Исключение содержит `Code`, `Action`, `RequestId` и необязательное `ErrorData` (`JsonElement?`).

Отмена запроса или таймаут не откатывают работу на сервере. Если запись уже
началась, отмена со стороны вызывающего кода не отменяет общий сокет.

## Уведомления {#notifications}

```csharp
using var created = client.On<MessageCreated>("message:created",
    message => Console.WriteLine(message.Text));

using var saved = client.OnAsync<MessageCreated>("message:created",
    async (message, ct) => await SaveMessageAsync(message, ct));

using var ping = client.On("system:tick", () => Console.WriteLine("tick"));
```

- Чтобы удалить подписку, освободите её (Dispose). Подписки сохраняются при переподключении.
- Callback-и выполняются по одному, вне потока чтения сокета, и могут ожидать запросы к
  тому же клиенту.
- Callback, уже выбранный для вызова, может выполниться и после отписки.
- Исключения передаются через `Error` и не останавливают доставку остальным.
- Типизированные подписки требуют поле `data`.

Callback-и не захватывают UI-контекст. Передавайте в него сами:

```csharp
var ui = SynchronizationContext.Current
    ?? throw new InvalidOperationException("Call from the UI thread.");
using var subscription = client.On<MessageCreated>("message:created",
    message => ui.Post(_ => UpdateView(message), null));
```

Медленные callback-и заполняют очередь уведомлений (`NotificationQueueCapacity`, 256).
Переполнение останавливает соединение, а не отбрасывает уведомления молча. При
разрыве соединения уведомления в очереди отбрасываются, а токен выполняющегося callback-а
отменяется. Рассылки, пришедшие во время разрыва, не воспроизводятся повторно; перезагрузите состояние
после переподключения.

## Аутентификация {#authentication}

### Вручную {#manual}

```csharp
await client.ConnectAsync(ct);
await client.AuthenticateAsync(accessToken, ct);
await client.RequestAsync("message:send", new { text = "Hello" }, ct);
await client.LogoutAsync(ct);
```

- `AuthenticateAsync` отправляет `auth:<token>` и ждёт `auth:success`. Отказ
  выбрасывает `DarkWsResponseException` с кодом `auth:failed`, действием `auth` и
  пустым id запроса.
- `LogoutAsync` отправляет `logout` и ждёт `logout:success`.
- Команды выполняются по одной, а ожидание каждого ответа ограничено `RequestTimeout`.
  Таймаут или отмена во время выполнения команды отбрасывает сокет, чтобы запоздавший
  ответ не завершил следующую команду.
- Ручная аутентификация не сохраняет токен для переподключения.

### Автоматически на каждом сокете {#automatic-on-every-socket}

```csharp
await using var client = new DarkWsClient(new DarkWsClientOptions {
    Endpoint = new Uri("wss://example.com/ws"),
    AuthenticationTokenProvider = ct => tokenStore.GetAccessTokenAsync(ct),
});
```

Провайдер возвращает `ValueTask<string?>` и вызывается для каждого нового сокета. Запросы
ждут успешной аутентификации. Когда провайдер настроен, любой сбой при
восстановлении сессии — постоянный отказ готовности: отсутствие токена, исключение в
провайдере, `auth:failed`, таймаут или обрыв сокета до `auth:success`. Состояние
становится `Disconnected` с указанием причины, и автоматические повторы прекращаются. Устраните причину и
вызовите `ConnectAsync`.

Вызов `LogoutAsync` отключает провайдер, даже если подтверждение потеряно или вызов
отклонён из-за `MaxPendingRequests`. Последующий успешный `AuthenticateAsync` снова
включает его.

### Учётные данные на уровне HTTP {#http-level-credentials}

`ConfigureWebSocketOptionsAsync` выполняется перед каждым upgrade и может задать заголовки,
cookie, сертификаты или прокси:

```csharp
ConfigureWebSocketOptionsAsync = async (socket, ct) => {
    var token = await tokenStore.GetAccessTokenAsync(ct);
    socket.SetRequestHeader("Authorization", $"Bearer {token}");
},
```

Используйте его, когда HTTP endpoint требует аутентификации до открытия сокета. HTTP-идентичность
и сессия DarkWS независимы: при выходе очищайте заголовки и cookie, которыми владеет приложение,
чтобы переподключение не восстановило старую идентичность.

Провайдер и callback-и конфигурации должны учитывать отмену и не должны вызывать
клиент, соединение которого они готовят.

## Жизненный цикл {#lifecycle}

`State` принимает одно из значений `Disconnected`, `Connecting`, `Connected`, `Reconnecting` и
`Disposed`. `StateChanged` сообщает о переходах с причиной сбоя в `Reason`; `Error`
сообщает о фоновых сбоях.

- Один экземпляр владеет одной серверной сессией и поддерживает параллельные вызовы. Для разных
  аккаунтов или endpoint используйте отдельные экземпляры.
- `ConnectAsync` присоединяется к одной общей попытке подключения. Отмена со стороны вызывающего кода прекращает только
  его ожидание.
- При сбоях транспорта клиент переподключается с экспоненциальным jitter до 30 секунд. Уже
  отправленные запросы завершаются ошибкой и никогда не воспроизводятся повторно; новые запросы ждут готовности.
- При `Reconnect = false` оборванный сокет остаётся закрытым, и последующие запросы завершаются ошибкой до
  явного вызова `ConnectAsync`.
- Неудачная автоматическая аутентификация, сбой callback-а сокета, HTTP 401 или 403,
  ошибки протокола и переполнение очереди уведомлений прекращают автоматические повторы.
- `CloseAsync` корректно закрывает соединение в пределах `CloseTimeout`, прекращает
  восстановление и отклоняет ожидающих до следующего `ConnectAsync`.
- `Dispose` и `DisposeAsync` сразу останавливают сетевую активность; `DisposeAsync`
  вдобавок ожидает завершения сетевых задач клиента. Для корректного закрытия сначала
  вызовите `CloseAsync`. Освобождение окончательно и идемпотентно.

Наблюдатели событий выполняются вне контекста сокета и UI; делайте их короткими. Исключения в
наблюдателях `Error` изолируются.

## Исключения {#exceptions}

| Исключение | Значение |
| --- | --- |
| `DarkWsResponseException` | Сервер ответил ошибкой |
| `DarkWsTimeoutException` | `Stage` — `Connection`, `Send` или `Response` |
| `DarkWsConnectionException` | Подключение не удалось или закрыто; `CloseStatus`, `CloseReason` |
| `DarkWsProtocolException` | Сервер нарушил протокол; `CloseStatus` |
| `DarkWsClientLimitException` | Достигнут `MaxPendingRequests` (ничего не отправлено) или переполнилась очередь уведомлений |
| `JsonException` | Не удалось преобразовать payload или результат |

## Опции {#options}

| Опция | По умолчанию | Назначение |
| --- | --- | --- |
| `Endpoint` | обязательна | URI `ws://` или `wss://` |
| `ConnectionTimeout` | 30 с | Открытие сокета |
| `SendTimeout` | 30 с | Запись сообщения |
| `RequestTimeout` | 5 мин | Ожидание ответа; `Timeout.InfiniteTimeSpan` отключает |
| `CloseTimeout` | 5 с | Корректное закрытие в `CloseAsync` |
| `Reconnect` | `true` | Переподключаться после сбоев транспорта |
| `PingInterval` / `PongTimeout` | по 30 с | Heartbeat |
| `MaxMessageSizeBytes` | 1 MiB | Максимальный размер входящего сообщения с учётом всех фрагментов |
| `MaxPendingRequests` | 256 | Ожидающие вызовы, включая аутентификацию и ожидание подключения |
| `NotificationQueueCapacity` | 256 | Уведомления в очереди до переполнения |
| `JsonOptions` | `JsonSerializerDefaults.Web` | Собственная копия опций сериализации |
| `ConfigureWebSocketOptionsAsync` | нет | Настройка каждого запроса upgrade |
| `AuthenticationTokenProvider` | нет | Токен для автоматической аутентификации |

Конечные таймауты должны быть от 1 до 4294967294 миллисекунд. Ожидания подключения, отправки и ответа
независимы, поэтому вызов может занять их сумму; для общего срока используйте токен отмены.
Backpressure на сервере может задерживать `pong`, поэтому настраивайте heartbeat под
свою нагрузку.

Клиент закрывает сокет со статусом 1003, если сервер отправляет бинарный фрейм.

## Внедрение зависимостей {#dependency-injection}

`DarkWS.Client.DependencyInjection` регистрирует клиент в Microsoft DI. Пакет
ссылается только на абстракции DI, а не на ASP.NET или Generic Host.

```bash
dotnet add package DarkWS.Client.DependencyInjection
```

```csharp
services.AddDarkWsClient(options => {
    options.Endpoint = new Uri("wss://example.com/ws");
});

public sealed class Calculator(IDarkWsClient client) {
    public Task<int> SumAsync(int left, int right) =>
        client.RequestAsync<int>("math:sum", new { left, right });
}
```

`AddDarkWsClient` регистрирует один ленивый singleton `IDarkWsClient`. Его разрешение не
подключает клиент; подключает первый запрос или `ConnectAsync`. Освобождением владеет контейнер:
потребители освобождают свои подписки, а не клиент. Повторные или конфликтующие
регистрации выбрасывают исключение до изменения коллекции; keyed-регистрация
`IDarkWsClient` тоже считается такой.

Настройка с использованием других сервисов:

```csharp
services.AddDarkWsClient((provider, options) => {
    options.Endpoint = new Uri("wss://example.com/ws");
    var tokens = provider.GetRequiredService<TokenStore>();
    options.AuthenticationTokenProvider = ct => tokens.GetAccessTokenAsync(ct);
});
```

`TokenStore` должен быть безопасен для использования из singleton. Не захватывайте scoped-сервисы в
callback-ах singleton.

### Одна сессия на область {#one-session-per-scope}

Singleton подходит для одной общей серверной идентичности. Для отдельной идентичности в каждой области
зарегистрируйте клиент сами:

```csharp
services.AddScoped<IDarkWsClient>(provider => {
    var tokens = provider.GetRequiredService<UserTokenStore>();
    return new DarkWsClient(new DarkWsClientOptions {
        Endpoint = new Uri("wss://example.com/ws"),
        AuthenticationTokenProvider = ct => tokens.GetAccessTokenAsync(ct),
    });
});
```

Согласуйте область со временем жизни сессии: область HTTP-запроса заканчивается вместе с
запросом. Не сочетайте эту регистрацию с `AddDarkWsClient`. Для нескольких
endpoint используйте keyed-регистрации или экземпляры, которыми владеет приложение.

## Поддержка платформ {#platform-support}

Native AOT, trimming, .NET Framework, Unity, браузерный WebAssembly и интеграция с жизненным циклом
мобильных приложений не сертифицированы.
