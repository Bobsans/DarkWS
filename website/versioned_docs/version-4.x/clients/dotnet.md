---
sidebar_position: 2
title: .NET client
---

# .NET client

`DarkWS.Client` is an async client for .NET 8, 9, and 10. It has no runtime package
dependencies and no ASP.NET requirement, so it works in console apps, services,
desktop apps, and integration tests.

```bash
dotnet add package DarkWS.Client --version 4.0.0
```

```csharp
using DarkWS.Client;

await using var client = new DarkWsClient(new Uri("wss://example.com/ws"));
var result = await client.RequestAsync<SumResult>("math:sum", new { left = 10, right = 20 });
Console.WriteLine(result.Value); // 30

public sealed record SumResult(int Value);
```

The first request opens the connection. Call `ConnectAsync(ct)` for an early
readiness check, or when the application only listens for broadcasts.

For full configuration, pass `DarkWsClientOptions`:

```csharp
await using var client = new DarkWsClient(new DarkWsClientOptions {
    Endpoint = new Uri("wss://example.com/ws"),
    RequestTimeout = TimeSpan.FromSeconds(30),
    AuthenticationTokenProvider = ct => tokenStore.GetAccessTokenAsync(ct),
});
```

## Requests

| Call | Sends | Returns |
| --- | --- | --- |
| `RequestAsync<T>(action, ct)` | No `data` | Deserialized `data` |
| `RequestAsync<T>(action, payload, ct)` | `payload` as `data` | Deserialized `data` |
| `RequestAsync(action, ct)` | No `data` | Completes on acknowledgement |
| `RequestAsync(action, payload, ct)` | `payload` as `data` | Completes on acknowledgement |

- Actions without a result still wait for acknowledgement and throw on server errors.
- `(object?)null` sends explicit JSON null; omitting the payload omits `data`.
- Typed requests require a `data` field in the response.
- Nullable annotations are not enforced at runtime; JSON null follows System.Text.Json
  rules.
- Use `JsonElement` for dynamic results. Returned elements stay valid after receive
  buffers are released.

A server error throws `DarkWsResponseException`:

```csharp
try {
    await client.RequestAsync("message:delete", new { id }, ct);
} catch (DarkWsResponseException error) when (error.Code == "message:forbidden") {
    ShowForbidden();
}
```

It exposes `Code`, `Action`, `RequestId`, and optional `ErrorData` (`JsonElement?`).

Cancelling a request or timing out does not undo server work. Once a write has
started, caller cancellation does not cancel the shared socket.

## Notifications

```csharp
using var created = client.On<MessageCreated>("message:created",
    message => Console.WriteLine(message.Text));

using var saved = client.OnAsync<MessageCreated>("message:created",
    async (message, ct) => await SaveMessageAsync(message, ct));

using var ping = client.On("system:tick", () => Console.WriteLine("tick"));
```

- Dispose a subscription to remove it. Subscriptions survive reconnects.
- Callbacks run one at a time, outside the socket reader, and may await requests on
  the same client.
- A callback already picked for invocation may still run after unsubscribe.
- Exceptions are reported through `Error` and do not stop delivery to others.
- Typed subscriptions require a `data` field.

Callbacks do not capture a UI context. Post to it yourself:

```csharp
var ui = SynchronizationContext.Current
    ?? throw new InvalidOperationException("Call from the UI thread.");
using var subscription = client.On<MessageCreated>("message:created",
    message => ui.Post(_ => UpdateView(message), null));
```

Slow callbacks fill the notification queue (`NotificationQueueCapacity`, 256).
Overflow stops the connection instead of silently dropping notifications. On
disconnect, queued notifications are discarded and the running callback's token is
cancelled. Broadcasts received while disconnected are not replayed; reload state
after reconnecting.

## Authentication

### Manual

```csharp
await client.ConnectAsync(ct);
await client.AuthenticateAsync(accessToken, ct);
await client.RequestAsync("message:send", new { text = "Hello" }, ct);
await client.LogoutAsync(ct);
```

- `AuthenticateAsync` sends `auth:<token>` and waits for `auth:success`. Rejection
  throws `DarkWsResponseException` with code `auth:failed`, action `auth`, and an
  empty request id.
- `LogoutAsync` sends `logout` and waits for `logout:success`.
- Commands run one at a time, and `RequestTimeout` bounds each reply. A timeout or
  cancellation while a command is in flight discards the socket, so a late reply
  cannot complete a later command.
- Manual authentication does not keep the token for reconnect.

### Automatic on every socket

```csharp
await using var client = new DarkWsClient(new DarkWsClientOptions {
    Endpoint = new Uri("wss://example.com/ws"),
    AuthenticationTokenProvider = ct => tokenStore.GetAccessTokenAsync(ct),
});
```

The provider returns `ValueTask<string?>` and runs for every fresh socket. Requests
wait until authentication succeeds. When the provider is configured, any failure
while restoring the session is a permanent readiness failure: a missing token, a
provider exception, `auth:failed`, a timeout, or a socket drop before `auth:success`.
The state becomes `Disconnected` with the reason, and automatic retries stop. Fix the
cause and call `ConnectAsync`.

Calling `LogoutAsync` disables the provider, even if the acknowledgement is lost or
the call is rejected by `MaxPendingRequests`. A later successful `AuthenticateAsync`
re-enables it.

### HTTP-level credentials

`ConfigureWebSocketOptionsAsync` runs before every upgrade and can set headers,
cookies, certificates, or a proxy:

```csharp
ConfigureWebSocketOptionsAsync = async (socket, ct) => {
    var token = await tokenStore.GetAccessTokenAsync(ct);
    socket.SetRequestHeader("Authorization", $"Bearer {token}");
},
```

Use it when the HTTP endpoint requires authentication before the socket opens. HTTP
identity and the DarkWS session are separate: clear application-owned headers and
cookies on logout so a reconnect cannot restore the old identity.

Provider and configuration callbacks must honor cancellation and must not call the
client whose connection they are preparing.

## Lifecycle

`State` is one of `Disconnected`, `Connecting`, `Connected`, `Reconnecting`, and
`Disposed`. `StateChanged` reports transitions with the failure in `Reason`; `Error`
reports background failures.

- One instance owns one server session and supports concurrent callers. Use separate
  instances for separate accounts or endpoints.
- `ConnectAsync` joins one shared connection attempt. Caller cancellation stops only
  that caller's wait.
- Transport failures reconnect with exponential jitter up to 30 seconds. Requests
  already sent fail and are never replayed; new requests wait for readiness.
- With `Reconnect = false`, a dropped socket stays down, and later requests fail until
  an explicit `ConnectAsync`.
- A failed automatic authentication, a failing socket callback, HTTP 401 or 403,
  protocol errors, and notification overflow stop automatic retry.
- `CloseAsync` closes gracefully within `CloseTimeout`, stops recovery, and rejects
  waiters until the next `ConnectAsync`.
- `Dispose` and `DisposeAsync` stop network activity at once; `DisposeAsync` also
  awaits the client's network tasks. Call `CloseAsync` first for a graceful close.
  Disposal is terminal and idempotent.

Event observers run off the socket and UI context; keep them short. Exceptions in
`Error` observers are contained.

## Exceptions

| Exception | Meaning |
| --- | --- |
| `DarkWsResponseException` | The server replied with an error |
| `DarkWsTimeoutException` | `Stage` is `Connection`, `Send`, or `Response` |
| `DarkWsConnectionException` | Connection failed or closed; `CloseStatus`, `CloseReason` |
| `DarkWsProtocolException` | The server violated the protocol; `CloseStatus` |
| `DarkWsClientLimitException` | `MaxPendingRequests` reached (nothing was sent), or the notification queue overflowed |
| `JsonException` | A payload or result could not be converted |

## Options

| Option | Default | Meaning |
| --- | --- | --- |
| `Endpoint` | required | `ws://` or `wss://` URI |
| `ConnectionTimeout` | 30 s | Opening the socket |
| `SendTimeout` | 30 s | Writing a message |
| `RequestTimeout` | 5 min | Waiting for a reply; `Timeout.InfiniteTimeSpan` disables it |
| `CloseTimeout` | 5 s | Graceful close in `CloseAsync` |
| `Reconnect` | `true` | Reconnect after transport failures |
| `PingInterval` / `PongTimeout` | 30 s each | Heartbeat |
| `MaxMessageSizeBytes` | 1 MiB | Largest incoming message, all fragments included |
| `MaxPendingRequests` | 256 | Pending calls, including authentication and connection waits |
| `NotificationQueueCapacity` | 256 | Queued notifications before overflow |
| `JsonOptions` | `JsonSerializerDefaults.Web` | Private copy of serialization options |
| `ConfigureWebSocketOptionsAsync` | none | Configure each upgrade request |
| `AuthenticationTokenProvider` | none | Token for automatic authentication |

Finite timeouts must be 1 to 4294967294 milliseconds. Connection, send, and reply
waits are separate, so a call can take their sum; use a cancellation token for an
overall deadline. Server backpressure can delay `pong`, so tune the heartbeat for
your workload.

The client closes the socket with status 1003 if the server sends a binary frame.

## Dependency injection

`DarkWS.Client.DependencyInjection` registers the client in Microsoft DI. It
references only DI abstractions, not ASP.NET or the Generic Host.

```bash
dotnet add package DarkWS.Client.DependencyInjection --version 4.0.0
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

`AddDarkWsClient` registers one lazy singleton `IDarkWsClient`. Resolving it does not
connect; the first request or `ConnectAsync` does. The container owns disposal:
consumers dispose their subscriptions, not the client. Duplicate or conflicting
registrations throw before changing the collection; a keyed `IDarkWsClient`
registration counts as one too.

Configure from other services:

```csharp
services.AddDarkWsClient((provider, options) => {
    options.Endpoint = new Uri("wss://example.com/ws");
    var tokens = provider.GetRequiredService<TokenStore>();
    options.AuthenticationTokenProvider = ct => tokens.GetAccessTokenAsync(ct);
});
```

`TokenStore` must be safe to use from a singleton. Do not capture scoped services in
singleton callbacks.

### One session per scope

A singleton suits one shared server identity. For a separate identity per scope,
register the client yourself:

```csharp
services.AddScoped<IDarkWsClient>(provider => {
    var tokens = provider.GetRequiredService<UserTokenStore>();
    return new DarkWsClient(new DarkWsClientOptions {
        Endpoint = new Uri("wss://example.com/ws"),
        AuthenticationTokenProvider = ct => tokens.GetAccessTokenAsync(ct),
    });
});
```

Match the scope to the session's lifetime: an HTTP request scope ends with the
request. Do not combine this registration with `AddDarkWsClient`. For several
endpoints, use keyed registrations or application-owned instances.

## Platform support

Native AOT, trimming, .NET Framework, Unity, browser WebAssembly, and mobile lifecycle
integration are not certified.
