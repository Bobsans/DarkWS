# DarkWS

ASP.NET Core WebSocket request/response library with typed sessions and an
in-memory broadcast backplane.

```csharp
builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<Program>()
    .AddAuthenticator<AppAuthenticator, AppSession>();

app.UseWebSockets();
app.MapDarkWs("/ws");
```

```csharp
public sealed record AppSession(
    string Id,
    ClaimsPrincipal User,
    Guid AccountId
) : IDarkWsSession {
    public IReadOnlyCollection<string> Groups => [$"account:{AccountId}"];
}

[Handler("message")]
public sealed class MessageHandler : HandlerBase<AppSession> {
    [Action("send")]
    public async Task<IResponse> SendAsync(MessageInput input) {
        await BroadcastToGroupAsync($"account:{Session.AccountId}", "message:created", input);
        return Ok();
    }
}
```

Handlers require an authenticated session by default. Add `[AllowAnonymous]`
to public handlers or actions. Register `AddSession` and call `UseSession`
before `MapDarkWs` when handlers need ASP.NET `ISession`.

## Incoming message limit

`DarkWsOptions.MaxMessageSizeBytes` limits a complete incoming message in bytes,
including all fragments, before JSON parsing. The default is 1 MiB (1048576 bytes);
values must be positive. Messages exactly at the limit are accepted. Exceeding
the limit closes the connection with status 1009 (Message Too Big), without
dispatching the partial message. This also applies to authentication messages.

```csharp
services.AddDarkWs(options => options.MaxMessageSizeBytes = 256 * 1024);
```

## Request concurrency and liveness

`MaxConcurrentRequestsPerConnection` defaults to 16 and must be positive. At
capacity, request dispatch waits for a running request before accepting more
work. Socket reads slow down; responses can arrive out of order and use `id`
for correlation. The host or reverse proxy must enforce total connection and
per-user/IP limits. Slow handlers can delay reading control messages at capacity.

`KeepAliveInterval` defaults to 30 seconds. On .NET 9/10, `KeepAliveTimeout`
(30 seconds) enables transport PING/PONG failure detection. On .NET 8,
`ReceiveIdleTimeout` (2 minutes) aborts a connection when a pending socket read
times out. Each received fragment resets this timer; it is inactive while
backpressure pauses reads. Idle clients must send application traffic such as
text `ping`; the browser client does so every 30 seconds by default. Transport
PONGs do not reset the application receive timer. All timeout options must be
positive and at most 4294967294 milliseconds.

## Registration contract

Call `AddDarkWs()` once and reuse its builder for additional handler assemblies.
Repeated calls throw `InvalidOperationException` without replacing the registry.
Only actions declared on the scanned handler class are registered; inherited
actions must be declared or overridden there and marked with `[Action]`.
Attributed methods must be public instance methods returning exactly `IResponse`
or `Task<IResponse>` with zero or one payload parameter. Unsupported signatures
(including generic methods and by-reference/byref-like/pointer payloads) throw
`InvalidOperationException` naming the type, method, and reason during registration.

## Options, payloads, and context

The standard `Configure`, configuration binding, and `PostConfigure` pipeline is
supported. Final options are validated on resolution and host startup with
`OptionsValidationException`. Existing connections and singleton services retain
their captured settings. Non-nullable parameters require a non-null payload;
nullable parameters permit omitted/null values. Invalid payloads return
`darkws:error:invalid-request` before constructing or invoking the handler.

An injected `IDarkWsContextAccessor` is initialized only inside a message scope.
Other scopes receive `InvalidOperationException` on property access. Lifecycle
middleware should use the context supplied to its hook.

## Shutdown and authentication

`SendTimeout` defaults to 30 seconds and includes send-lock wait and transport
write. Expiration aborts the socket. `ShutdownTimeout` bounds the whole shutdown:
pending tasks, close hooks, and handshake. Connections leave storage immediately;
handler tokens are cancelled. Handlers must cooperate with cancellation. Tasks
that ignore it retain their scope/connection resources until actual completion,
while the socket is aborted and further responses are suppressed.

Reserved requests `darkws:authenticate` (string token payload) and `darkws:logout`
receive correlated acknowledgements. Rejected authentication clears the previous
session and returns `darkws:error:authentication-failed`; logout clears it without
closing the socket. `OnAuthenticatedAsync` runs after success, rejection, and
logout, and its current session can be null. Legacy `auth:<token>` remains accepted
and now replies with id `@auth`. Token expiry and revocation enforcement remain
the application's responsibility; already running actions are not rolled back.

Session and group indexes refresh on registration and re-authentication. Re-add
the connection with `ConnectionStorage.Add` after external group changes.
XML API documentation and `.snupkg` symbols with embedded sources are included.

## Compatibility and diagnostics

See the repository CHANGELOG before upgrading from 2.0.0: 2.1.0 introduced the
1 MiB default limit and closes oversized input with status 1009. Configure
`MaxMessageSizeBytes` explicitly if your application legitimately needs more.

Request ids `@` and `@auth` are reserved. Invalid requests using them receive
`invalid-request` with an empty id, so errors cannot be mistaken for broadcasts.
Empty response error codes are rejected at construction. Domain exceptions retain
their code in `Message` and support an optional inner exception. Public object
boundaries report null arguments with `ArgumentNullException`.

Successful actions and malformed requests use Debug logs; unexpected failures
remain warnings. Static callers should use `DarkWsServiceCollectionExtensions`
and `DarkWsEndpointRouteBuilderExtensions`. The old `Configuration` wrapper is
obsolete; extension-call syntax remains unchanged.

Native AOT and trimmed publishing are unsupported because handler discovery,
delegate compilation, and JSON serialization depend on reflection/runtime code
generation. Use ordinary JIT publishing. The .NET 8/9/10 targets are intentional:
their liveness mechanisms differ.

Only `[AllowAnonymous]` is supported for action authorization. `[Authorize]` and
other `IAuthorizeData` on handlers/actions fail registration; implement domain
role/policy checks inside handlers and return controlled errors on denial.

Query-string tokens can be recorded by proxies and access logs. Use short-lived
tickets or, where the host/authenticator permits an anonymous upgrade, authenticate
after connecting instead of putting credentials in the URL. Use WSS, redact
credential logging, and enforce token lifetime/revocation in the application.
