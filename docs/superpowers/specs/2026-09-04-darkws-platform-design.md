# DarkWS Platform Design

## Goal

Turn the current `DarkBoy.DarkWS` server into a reusable ASP.NET Core WebSocket
request/response library with typed sessions, a default in-memory backplane, an
optional Redis backplane package, and a standalone browser client published as
`@darkboy/darkws`.

The design takes proven protocol and reliability behavior from the local
Fixdigital implementation without taking any Fixdigital business types,
services, application wrappers, localization, logging integrations, or UI
behavior.

## Deliverables

```text
DarkWS/
├── DarkBoy.DarkWS/                 # ASP.NET Core server and in-memory backplane
├── DarkBoy.DarkWS.Redis/           # Optional Redis backplane
├── DarkBoy.DarkWS.Test/            # Server and in-memory tests
├── DarkBoy.DarkWS.Redis.Test/      # Redis integration tests
├── packages/
│   └── darkws/                     # @darkboy/darkws browser package
└── DarkBoy.DarkWS.sln
```

`DarkBoy.DarkWS` keeps its current package version during development. The new
Redis and npm packages start at `1.0.0`. Publishing, remote configuration, and
high-level application adapters are not part of this change.

## Wire Protocol

The server and browser package share this JSON contract:

```text
request:   { "id": string, "action": string, "payload"?: unknown }
success:   { "id": string, "data"?: unknown }
error:     { "id": string, "error": string, "data"?: unknown }
broadcast: { "id": "@", "data": { "action": string, "data"?: unknown } }
```

The server must rename its current request property from `Data` to `Payload`.
JSON serialization remains configurable through `DarkWsOptions`, but the
protocol field names above must remain stable.

Plain-text control messages remain:

- `ping` from client, `pong` from server.
- `auth:<token>` for authentication or re-authentication on an open socket.

## ASP.NET Core Integration

Registration uses explicit assemblies instead of scanning every loaded
assembly:

```csharp
builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<Program>()
    .AddAuthenticator<AppAuthenticator, AppSession>();

app.UseWebSockets();
app.MapDarkWs("/ws");
```

`MapDarkWs` maps one ASP.NET Core endpoint and keeps the WebSocket connection
inside the normal middleware pipeline. The endpoint therefore retains
`HttpContext`, `HttpContext.User`, request cancellation, and ASP.NET session
features installed by the host.

Handler discovery must tolerate partially loadable assemblies and reject
duplicate action keys during registration. Action descriptors belong to a
DI-owned registry, not a process-global static dictionary, so multiple hosts
and tests cannot contaminate one another.

Every request message creates a new async DI scope. The dispatcher resolves the
handler and request context from that scope. Background action tasks are
tracked and receive a bounded graceful-shutdown window when the socket closes.

## Authentication and Session Context

The generic session contract contains only reusable routing and identity data:

```csharp
public interface IDarkWsSession {
    string Id { get; }
    ClaimsPrincipal User { get; }
    IReadOnlyCollection<string> Groups { get; }
}
```

Applications implement a typed session record with their own immutable data:

```csharp
public sealed record AppSession(
    string Id,
    ClaimsPrincipal User,
    int TenantId,
    Guid UserId
) : IDarkWsSession {
    public IReadOnlyCollection<string> Groups =>
        [$"tenant:{TenantId}", $"user:{UserId}"];
}
```

`HandlerBase<TSession>` exposes the typed `Session`, `HttpContext`, optional
ASP.NET `ISession`, current connection, and connection cancellation token.
`HandlerBase` remains available for anonymous handlers.

Scoped application services access the same values through
`IDarkWsContextAccessor`. The accessor exposes `IDarkWsSession?`, `HttpContext`,
optional ASP.NET `ISession`, current connection, and cancellation token.

`IDarkWsAuthenticator<TSession>` accepts `HttpContext`, a token, and a
cancellation token. Successful initial authentication or `auth:<token>` sets
the connection session and replaces `HttpContext.User` with the session
principal. Re-authentication invokes middleware after the replacement.

Authentication is required for handlers and actions by default.
`[AllowAnonymous]` explicitly opts a handler or action out. Policy-specific
authorization is performed by application code through ASP.NET
`IAuthorizationService`; the core package does not duplicate policy evaluation.

ASP.NET `ISession` is optional. Hosts that need it register `AddSession` and
place `UseSession` before `MapDarkWs`. Because a WebSocket is a long-lived HTTP
request, a handler that changes ASP.NET session data and needs immediate
persistence must call `CommitAsync` explicitly.

## Connection Lifecycle

The server adopts these reliability rules:

- Store connections in a `ConcurrentDictionary`.
- Serialize writes per connection with `SemaphoreSlim`.
- Bound every broadcast send to ten seconds by default.
- Abort a connection when a broadcast send exceeds the configured timeout.
- Rent receive buffers from `ArrayPool<byte>`.
- Link connection cancellation to `HttpContext.RequestAborted` and application
  shutdown.
- Invoke `DarkWsMiddleware` hooks for open, successful authentication, and
  close.
- Log unhandled exceptions through `ILogger`, but return only the configured
  generic request-failed error to clients.
- Return stable errors for missing authorization and invalid actions.

The existing tracked action tasks, graceful close, configurable JSON options,
configurable endpoint path, and explicit connection disposal remain.

## Broadcast Backplane

The core package defines a public `IDarkWsBackplane` contract because the Redis
package implements it. A backplane publishes and subscribes to a
`DarkWsBroadcast` envelope:

```csharp
public sealed record DarkWsBroadcast(
    DarkWsTarget Target,
    string? TargetId,
    string Action,
    JsonElement? Data
);

public enum DarkWsTarget {
    All,
    Connection,
    Session,
    Group
}
```

`InMemoryDarkWsBackplane` is a singleton and the default implementation. It
delivers each published envelope only to connections held by the current
process.

The broadcaster exposes:

```text
BroadcastAsync
BroadcastToConnectionAsync
BroadcastToSessionAsync
BroadcastToGroupAsync
```

Session and group routing comes from `IDarkWsSession.Id` and
`IDarkWsSession.Groups`. The core API contains no tenant or user-specific
methods. Applications express those concepts through group strings.

Per-connection data factories remain local-only and are not part of the
backplane API because delegates cannot cross process boundaries.

## Redis Package

`DarkBoy.DarkWS.Redis` references `DarkBoy.DarkWS` and
`StackExchange.Redis`. It does not contain a connection string or create a
separate Redis connection. The host registers `IConnectionMultiplexer` and
selects an explicit channel:

```csharp
builder.Services.AddDarkWsRedis("my-app:production");
```

`AddDarkWsRedis` replaces the default `IDarkWsBackplane`. A hosted service owns
subscription start and disposal. A published Redis message is handled once by
each application instance and then routed only to that instance's local
connections. Receiving a Redis message never republishes it.

Channel names are explicit to prevent different applications or environments
from sharing traffic accidentally.

## Browser Package

`packages/darkws` publishes `@darkboy/darkws` as an ESM browser package with
TypeScript declarations and no runtime dependencies. `tsc` produces the
publishable JavaScript and declaration files; no bundler is added.

The low-level `DarkWs` class supports:

- connection state and typed lifecycle events;
- request/response correlation;
- `ErrorResponse`, `ConnectionClosedError`, and `RequestTimeoutError`;
- request and connection-wait timeouts;
- reconnect after clean or abnormal server closes;
- exponential reconnect delay with jitter;
- immediate `reconnect()`;
- `beforeConnect` and dynamic query hooks;
- `authenticate(token)` using `auth:<token>`;
- ping scheduling;
- broadcast subscriptions;
- `close()` and terminal `dispose()`.

Extraction removes the local `@/shared/utils` and `lodash-es` imports.
`crypto.randomUUID`, `typeof`, and global timers replace them. `dispose()`
cancels ping and reconnect timers and rejects all pending requests. Empty query
parameters do not add a trailing `?`.

The low-level package does not automatically retry requests. It cannot know
whether an action is idempotent. Applications may retry known reads outside
the transport.

A high-level interface will later be added to the same npm package as new named
exports. No speculative adapter or empty extension interface is created now;
the low-level exports remain compatible with that future addition.

## Handler Usage

Handlers use constructor injection for scoped application services and inherit
the typed session:

```csharp
[Handler("client")]
public sealed class ClientHandler(ClientService clients)
    : HandlerBase<AppSession> {
    [Action("sync")]
    public async Task<IResponse> SyncAsync(ClientInput input) {
        var result = await clients.SyncAsync(input, Session.UserId);
        await BroadcastToGroupAsync(
            $"tenant:{Session.TenantId}",
            "client:sync",
            result
        );
        return Ok(result);
    }
}
```

The handler has no dependency on Redis. Switching backplanes changes only host
registration.

## Error Handling

- Malformed request JSON returns a stable invalid-request error when a request
  id can be recovered; otherwise it is logged and ignored.
- Unknown actions return a stable invalid-action error.
- Missing authentication returns a stable authorization-required error.
- `DarkWsException` continues to produce an intentional error response.
- Other exceptions are logged and return a configurable generic request-failed
  error without exception messages or stack traces.
- Client request timeouts remove their resolver before rejection.
- Socket close or replacement rejects every resolver owned by that socket with
  `ConnectionClosedError`.

## Verification

Server and in-memory tests cover:

- ASP.NET endpoint mapping;
- `HttpContext`, optional `ISession`, typed WebSocket session, and scoped
  context access;
- authentication required by default and `[AllowAnonymous]` opt-out;
- `auth:<token>` re-authentication;
- handler discovery and duplicate action rejection;
- `payload` deserialization and all response shapes;
- concurrent send serialization;
- all, connection, session, and group broadcasts;
- send timeout and graceful shutdown;
- generic error responses without exception leakage.

Redis integration tests run two service providers against one disposable Redis
instance and verify cross-instance delivery without republishing loops.

Browser tests cover URL generation, open/close/error/message events, clean and
abnormal reconnects, exponential jitter, request correlation, request timeout,
pending-request rejection, authentication, broadcast delivery, close, and
dispose. Package verification runs `npm test`, `npm run build`, and
`npm pack --dry-run`.

Final verification runs the complete .NET build and test suite for `net8.0`,
`net9.0`, and `net10.0`, then verifies both NuGet packages can be packed.

## Rollback

All work remains uncommitted unless explicitly requested. Rollback can restore
the current target files and remove the new Redis and npm package directories.
The Fixdigital repository remains read-only throughout this work.
