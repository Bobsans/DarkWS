# DarkWS

ASP.NET Core WebSocket request/response library with typed sessions and an
in-memory broadcast backplane.

`BroadcastToGroupsAsync(groups, action, data, except: new DarkWsBroadcastExclusion { ConnectionId = Connection.Id })`
publishes once to a group union and excludes the calling connection. Use `SessionId`
to exclude all connections in a session, or set both ids. The no-data overload and
matching `BroadcastToGroupAsync(..., except: ...)` overloads are also available on
`IBroadcaster` and `HandlerBase`. Use the named `except` argument for exclusions.
Overlapping groups receive one notification per connection. Empty groups publish
nothing; null collections and blank group names/exclusion ids are invalid.
Upgrade all Redis-connected nodes before using the new Groups=4 envelope; old nodes
skip these messages. Existing single-target broadcasts keep their wire format.

```csharp
builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<Program>()
    .AddAuthenticator<AppAuthenticator, AppSession>();

app.UseWebSockets(new WebSocketOptions { AllowedOrigins = { "https://app.example.com" } });
app.MapDarkWs("/ws");
```

CORS does not apply to WebSockets: with cookie authentication, restrict
`WebSocketOptions.AllowedOrigins` so another site cannot open a socket that carries
the user's cookie. Other origins receive 403 before DarkWS runs; an empty list
allows every origin.

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
before `MapDarkWs` when handlers need ASP.NET `ISession`. Concurrent actions of
one connection share its `HttpContext`, `Items`, and `ISession`, which are not
thread-safe: do not modify them concurrently.

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

`MaxConcurrentRequestsPerConnection` defaults to 16 and must be between 1 and
`int.MaxValue - 4`. Up to that many requests run and as many ordinary requests wait
in FIFO order. The shared queue has four additional places reserved for
`auth:`/`logout`, so a queued command does not stop reading `ping` or transport
PONGs. Commands keep their order relative to requests; if one arrives at a full
shared queue, the connection closes with status 1008 (Policy Violation).
Ordinary requests can pause reads while waiting for a request place or shared
queue space. After `RequestQueueTimeout` (5 seconds) they receive `BusyError`
(`darkws:error:busy`), and reading continues. Keep that timeout below
`KeepAliveTimeout` and client pong timeouts. Responses can arrive out of order and
use `id` for correlation.
The host or reverse proxy must enforce total connection and per-user/IP limits.

Set `RunActionsOnThreadPool = true` (default `false`) to prevent synchronous
handlers, including code before the first `await`, from blocking dispatch of
later requests and `auth:`/`logout`. Scheduled actions still consume request slots;
queue backpressure and command ordering are unchanged. Each action captures its
DarkWS session before scheduling, but handlers may execute out of order.
Shared `HttpContext` and `ISession` remain unsafe for concurrent mutation.

`KeepAliveInterval` defaults to 30 seconds. On .NET 9/10, `KeepAliveTimeout`
(30 seconds) enables transport PING/PONG failure detection. On .NET 8,
`ReceiveIdleTimeout` (2 minutes) aborts a connection when a pending socket read
times out. Each received fragment resets this timer; it is inactive while a full
request queue pauses reads. Idle clients must send application traffic such as
text `ping`; the browser client does so every 30 seconds by default. Transport
PONGs do not reset the application receive timer. All timeout options must be
positive and at most 4294967294 milliseconds.

## Registration contract

Call `AddDarkWs()` once and reuse its builder for additional handler assemblies.
Repeated calls throw `InvalidOperationException` without replacing the registry.
Call `AddAuthenticator<TAuthenticator, TSession>()` once on that builder. A second
call throws `InvalidOperationException` before changing any registrations, even
when the authenticator and session types are the same.
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
their captured settings. Non-nullable parameters require a non-null payload by
default; nullable parameters permit omitted/null values. Set `AllowNullPayloads = true`
to ignore reference parameter nullability annotations and let handlers receive null,
including when `data` is omitted. It does not allow CLR null for non-nullable value
types or disable JSON type/range validation. Invalid payloads return
`darkws:error:invalid-request` before constructing or invoking the handler.

An injected `IDarkWsContextAccessor` is initialized only inside a message scope.
Other scopes receive `InvalidOperationException` on property access. Lifecycle
middleware should use the context supplied to its hook.

## Action filters and metadata

Register global filters with `AddActionFilter<TFilter>()`. They are scoped per
message and run in registration order, with the first filter outermost. A filter
may return an `IResponse` without calling `next`, or inspect the response or
exception from `next`. Call `next` at most once.

```csharp
services.AddDarkWs()
    .AddHandlersFromAssemblyContaining<MessageHandler>()
    .AddActionFilter<AppActionFilter>();
```

Filters run after action lookup, authorization, payload binding, and scope
initializers. They do not run for malformed JSON, unknown actions, unauthorized
requests, or invalid payloads. `DarkWsActionContext` exposes the registered
action name, handler type, method and method attributes through `Action`, plus
the deserialized `Payload`, captured `Session`, message `Services`, and
`CancellationToken`. `IDarkWsContextAccessor.Action` exposes the same metadata
to scope initializers and scoped services; it is null in connection lifecycle
hooks. `HandlerBase.Services` is the current message scope's service provider.

## Response handling

Results are serialized before the message scope is disposed, so deferred data over
a scoped service is still readable. A `null` result, a result that cannot be
serialized, or a custom `IResponse` that fails before sending is answered with
`darkws:error:request-failed` and logged; the connection stays open.

## Shutdown and authentication

`SendTimeout` defaults to 30 seconds and includes send-lock wait and transport
write. Expiration aborts the socket. `ShutdownTimeout` bounds the whole shutdown:
pending tasks, close hooks, and handshake. Connections leave storage immediately;
handler tokens are cancelled. Handlers must cooperate with cancellation. Tasks
that ignore it retain their scope/connection resources until actual completion,
while the socket is aborted and further responses are suppressed. In
`OnCloseAsync`, `ConnectionAborted` is the shutdown deadline, not the cancelled
handler token.

System commands are plain text: `auth:<token>` receives `auth:success` or
`auth:failed`; `logout` receives `logout:success`; `ping` receives `pong`.
Rejected authentication clears the previous session by default.
`KeepSessionOnFailedAuthentication = true` retains the session, HTTP principal, and
membership indexes after a rejected/empty token or authenticator exception, while
still replying `auth:failed`. No `OnAuthenticatedAsync` hook runs for an unchanged
session. Successful authentication replaces the session; explicit logout always
clears it. Both notify `OnAuthenticatedAsync`, as does rejection with the default
setting, and the hook's current session can be null.

During upgrade, authenticator exceptions are logged and return HTTP 401 by default.
`AcceptAnonymousOnUpgradeAuthenticationException = true` instead accepts an anonymous
connection with no session. A null authenticator result continues to allow anonymous
upgrade regardless of this option. Request/shutdown cancellation is propagated.
JSON authentication/logout actions and `@auth` replies are no longer used.
The obsolete `AuthenticationFailedError` option is unused and no longer validated;
empty values do not prevent startup. Remove it from configuration and code: text
authentication always replies `auth:failed`, and the option will be removed in the
next major version.
Token expiry and revocation enforcement remain the application's responsibility,
including when a failed refresh retains the previous session;
already running actions are not rolled back.

Session and group indexes refresh on registration and re-authentication. Re-add
the connection with `ConnectionStorage.Add` after external group changes; closed
connections are ignored, so a refresh racing with disconnect cannot re-register them.
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

Only `[AllowAnonymous]` is supported for built-in action authorization.
`[Authorize]` and other `IAuthorizeData` on handlers/actions fail registration;
implement domain role/policy checks in handlers or action filters and return
controlled errors on denial.

Query-string tokens can be recorded by proxies and access logs. Use short-lived
tickets or, where the host/authenticator permits an anonymous upgrade, authenticate
after connecting instead of putting credentials in the URL. Use WSS, redact
credential logging, and enforce token lifetime/revocation in the application.
