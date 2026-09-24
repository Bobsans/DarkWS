# DarkWS.Client

Async .NET client for DarkWS on .NET 8, 9, and 10. No runtime package dependencies
or ASP.NET requirement. Install with `dotnet add package DarkWS.Client`.

## Requests

```csharp
using DarkWS.Client;

await using var client = new DarkWsClient(new Uri("wss://example.com/ws"));
var result = await client.RequestAsync<SumResult>(
    "math:sum", new { left = 10, right = 20 });
Console.WriteLine(result.Value);

public sealed record SumResult(int Value);
```

The first request opens the connection. Use `ConnectAsync(ct)` for an early
readiness check or an application that only listens for broadcasts. The example
assumes an anonymous action returning `{ "value": 30 }`.

Use `RequestAsync("action", payload, ct)` for actions without a result: it still
waits for acknowledgement and checks server errors. Omitting payload omits its
wire `data` property; `(object?)null` sends explicit JSON null. Typed requests and
`On<T>` subscriptions require a `data` field; the DarkWS server always writes it
for data results and broadcasts, as JSON null for a null value, which becomes
`default`. JSON null follows System.Text.Json rules; nullable reference
annotations do not enforce runtime validation. Use `JsonElement` for dynamic
results; returned elements remain valid after receive buffers are released.

The wire schema is `{ id, action, data? }` for requests and
`{ id: "@", action, data? }` for broadcasts. Update the server and clients together;
the previous request `payload` field and nested broadcasts are incompatible.

## Notifications

```csharp
using var subscription = client.On<MessageCreated>(
    "message:created", message => Console.WriteLine(message.Text));
using var backgroundSubscription = client.OnAsync<MessageCreated>(
    "message:created", async (message, ct) =>
        await SaveMessageAsync(message, ct));
```

`On("action", () => ...)` supports payload-free notifications. Dispose subscriptions
when consumers go away. Subscriptions survive reconnect. Callbacks are serialized
outside the socket reader and may await requests on the same client. A callback
already acquired for invocation may finish after unsubscribe. Exceptions are
reported through `Error` without preventing delivery to other subscribers.

Callbacks do not capture a UI context. Capture and post to the application's UI
context explicitly, for example:

```csharp
var ui = SynchronizationContext.Current
    ?? throw new InvalidOperationException("Call from the UI thread.");
using var subscription = client.On<MessageCreated>("message:created", message =>
    ui.Post(_ => UpdateView(message), null));
```

Slow callbacks fill the bounded notification queue; overflow stops the connection
cycle instead of silently dropping arbitrary notifications. Disconnect discards
queued old notifications and cancels the running callback token. Callbacks should
honor cancellation. Disposal does not wait indefinitely for user code. There is
no replay of broadcasts received while disconnected; refresh state after reconnect.

## Authentication

```csharp
await client.ConnectAsync(ct);
await client.AuthenticateAsync(accessToken, ct);
await client.RequestAsync("message:send", new { text = "Hello" }, ct);
await client.LogoutAsync(ct);
```

Authentication sends text `auth:<token>` and waits for `auth:success`; rejection
`auth:failed` raises `DarkWsResponseException` with code `auth:failed`, action `auth`,
and an empty request id. Logout sends `logout` and waits for `logout:success`.
These commands run sequentially. A response timeout or cancellation while a command
is in flight discards the socket, preventing a late reply from completing a later
command; a command cancelled or timed out while still queued behind other writes
was never sent, so the socket stays in use. Manual authentication does not retain
the token for reconnect.
To restore the application session on each fresh socket:

```csharp
await using var client = new DarkWsClient(new DarkWsClientOptions {
    Endpoint = new Uri("wss://example.com/ws"),
    AuthenticationTokenProvider = ct => tokenStore.GetAccessTokenAsync(ct)
});
```

The provider returns `ValueTask<string?>`; when configured, a missing token fails
readiness. Requests wait for automatic authentication. The application owns
storage, refresh, expiry, and revocation. Callbacks must honor cancellation and
must not call methods on the client whose connection they are preparing.

`ConfigureWebSocketOptionsAsync` runs before every upgrade and may fetch current
headers/cookies. Use it if the HTTP endpoint requires authentication before opening
a socket. HTTP identity and DarkWS session authentication are distinct concerns.

Starting logout disables the session token provider even if acknowledgement is
lost. Update your source and successfully call `AuthenticateAsync` to re-enable
it. Clear application-owned upgrade headers/cookies separately so reconnect cannot
restore the old HTTP identity. Logout does not roll back running server actions.

## Lifecycle and errors

- One instance owns one server session; concurrent callers are supported. Use
  separate instances for separate accounts or endpoints.
- `ConnectAsync` joins one shared connection attempt. Caller cancellation stops
  only that caller's wait. Connection attempts continue until close/disposal or
  a permanent failure.
- Transport failures reconnect with exponential jitter up to 30 seconds, including a
  drop or timeout while automatic authentication waits for `auth:success`. Sent
  requests fail and are never replayed. New requests during recovery await readiness.
- Credentials rejected by the server (`auth:failed`), a failing or empty token
  provider, a failing socket configuration callback, HTTP 401/403, wire errors, and
  notification overflow stop automatic retry. Correct the cause and call `ConnectAsync`.
- `CloseAsync` stops recovery and rejects waiters. Requests fail until explicit
  `ConnectAsync`. Disposal is terminal and idempotent. `DisposeAsync` first closes an
  open connection gracefully, taking up to `CloseTimeout` (a connection attempt or an
  already running close is interrupted), then awaits network cleanup; `Dispose` aborts
  immediately for ordinary container disposal.
- Cancelling/timing out a request does not undo server work. Once a write starts,
  caller cancellation does not cancel the shared socket. A transport write timeout
  aborts the connection and fails its remaining requests.
- `DarkWsResponseException` exposes `Code`, `Action`, `RequestId`, and optional
  `ErrorData`. Timeout exceptions expose Connection, Send, or Response in `Stage`.
  Connection, protocol, and capacity failures have corresponding DarkWs exception
  types; a transport failure message names the cause chain by exception type and
  error code (for example `SocketError.ConnectionRefused`), never by its text, which
  could contain the endpoint's query token. Payload/result conversion uses `JsonException`.
- Observe `StateChanged` and `Error` for background failures. Request failures are
  returned through their tasks. Event observers run off the socket/UI context;
  keep them short. Exceptions in `Error` observers are contained.

## Defaults and constraints

| Setting | Default |
| --- | --- |
| ConnectionTimeout / SendTimeout | 30 seconds each |
| RequestTimeout | 5 minutes; `Timeout.InfiniteTimeSpan` disables response expiry |
| CloseTimeout | 5 seconds |
| Reconnect | true |
| PingInterval / PongTimeout | 30 seconds each |
| MaxMessageSizeBytes | 1 MiB across all fragments |
| MaxPendingRequests | 256, including application authentication/connection waits |
| NotificationQueueCapacity | 256 |
| JsonOptions | Private copy of `JsonSerializerDefaults.Web` options |

Finite timeouts must be 1 through 4294967294 milliseconds. Connection, send, and
response waits are separate; the total can reach their sum. Use a cancellation
token for an overall deadline. Text ping/pong works on every supported target;
server backpressure may delay pong, so tune deadlines for the workload.

Use WSS in production. The client never disables certificate validation by default.
Do not log tokens, payloads, query URLs, peer close reasons, or server error data
without appropriate redaction. TLS/proxy/header choices remain application-owned.

Install `DarkWS.Client.DependencyInjection` for optional `AddDarkWsClient`
registration. Direct construction and other containers only require this package.
Native AOT, trimming, .NET Framework, Unity, browser WebAssembly, and mobile
lifecycle integration are not certified by this release.
