---
sidebar_position: 1
title: Handlers and actions
---

# Handlers and actions

A handler is a class marked with `[Handler(name)]` that derives from `HandlerBase`.
Its public methods marked with `[Action(name)]` are actions. A client calls an action
by the combined name `handler:action`.

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

## Registration

Register handlers when you add DarkWS:

```csharp
builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<Program>()
    .AddHandlersFromAssembly(typeof(ExternalHandler).Assembly);
```

Registration scans the assembly for non-abstract classes derived from `HandlerBase`
and validates every action. Invalid declarations fail at startup with an
`InvalidOperationException` that names the type, the method, and the reason.

- Call `AddDarkWs()` once per service collection; a second call throws. Reuse the
  returned `DarkWsBuilder` for more assemblies, the authenticator, and scope
  initializers.
- Two actions with the same combined name fail registration. A handler without
  `[Handler]` exposes its actions under the bare action name.
- Actions must be public instance methods declared on the scanned class. Inherited
  methods are not scanned: declare or override the action on the concrete handler
  and mark it with `[Action]`.
- An action returns exactly `IResponse` or `Task<IResponse>`.
- An action accepts zero or one payload parameter. Generic methods and
  by-reference, byref-like, or pointer parameters are rejected.
- Assembly scanning also registers every non-abstract `DarkWsMiddleware` subclass
  (see [Middleware](pipeline.md#middleware)). Scope initializers are registered
  explicitly with `AddScopeInitializer<T>()`.

## Dependencies and scope

Each request runs in a new asynchronous DI scope. The handler is created from that
scope, so constructor injection of scoped services such as a `DbContext` is safe.
The scope is disposed when the action returns, before the result is written.

Inside an action, `HandlerBase` exposes the request context:

| Member | Meaning |
| --- | --- |
| `Connection` | The current `IWebSocketConnection` (`Id`, `Session`, `IsOpen`, `HttpContext`, `SendAsync`, `CloseAsync`) |
| `Session` | The connection's current session; throws for an anonymous connection |
| `HttpContext` | The upgrade request's `HttpContext`, shared by the connection |
| `AspNetSession` | ASP.NET `ISession` when session middleware is installed, otherwise null |
| `ConnectionAborted` | Cancelled when the connection stops; pass it to async work |

`HandlerBase` also provides the broadcast methods `BroadcastAsync`,
`BroadcastToSessionAsync`, `BroadcastToGroupAsync`, and `BroadcastToSelfAsync` for
the calling connection; see [Broadcasts](broadcasts.md).

Derive from `HandlerBase<TSession>` to get a typed `Session`; see
[Authentication and sessions](authentication.md).

Services outside handlers can inject `IDarkWsContextAccessor` for the same data;
see [Scope initializers and middleware](pipeline.md#context-accessor).

## Payloads

The request `data` field is deserialized into the action parameter with
`DarkWsOptions.JsonOptions` (web defaults: camelCase, case-insensitive).

- A nullable parameter (`string?`, `Input?`, `int?`) accepts a missing or null payload.
- A non-nullable parameter requires a payload. Omitted or null data returns
  `darkws:error:invalid-request` without invoking the handler.
- Type mismatches and numeric overflow in `data` return
  `darkws:error:invalid-request` before the handler is constructed.
- A message that is not valid JSON, or has no `id` or `action`, is answered with
  `darkws:error:invalid-request` only when its `id` can be read; otherwise it gets
  no reply.
- Use `JsonElement` to accept arbitrary JSON.

## Results

Return one of the helpers:

| Helper | Wire response |
| --- | --- |
| `Ok()` | `{ "id": "…" }` |
| `Ok(value)` | `{ "id": "…", "data": value }` |
| `Error("code")` | `{ "id": "…", "error": "code" }` |
| `Error("code", details)` | `{ "id": "…", "error": "code", "data": details }` |

Error codes must be non-empty. Use stable, namespaced codes such as
`message:not-found` that clients can switch on.

Results are serialized after the message scope is disposed. Materialize data that
depends on scoped services (for example, call `ToListAsync()` on a query) before
returning it. A `null` result, a value that cannot be serialized, or a custom
`IResponse` that fails is logged as a warning, and the client receives no reply
for that request. The connection stays open.

To write a custom response, implement `IResponse.WriteResultAsync(ResponseContext, CancellationToken)`
and send with `context.SendAsync(data)`.

## Errors

Throw `ErrorResponseException` from anywhere in the call chain to return a
controlled error:

```csharp
if (!await permissions.CanEditAsync(Session.UserId, id)) {
    throw new ErrorResponseException("message:forbidden");
}
throw new ErrorResponseException<ValidationDetails>("message:invalid", details);
```

Any other exception is logged as a warning and answered with
`darkws:error:request-failed`. Its message is never sent to the client. To create
your own exception type with a protocol response, derive from `DarkWsException` and
implement `GetResponse()`.

Built-in error codes are listed in [Protocol](../protocol.md).

## Authorization

Handlers require an authenticated session by default. Mark a handler or a single
action with `[AllowAnonymous]` to make it public:

```csharp
[Handler("system"), AllowAnonymous]
public sealed class SystemHandler : HandlerBase {
    [Action("ping")]
    public IResponse Ping() => Ok(new { ServerTime = DateTimeOffset.UtcNow });
}
```

A call to a protected action without a session, or with a session whose `User` is
not authenticated, returns `darkws:error:authorization-required`.

Built-in authorization supports only this authenticated/anonymous distinction.
`[Authorize]`, role and policy attributes, and any other `IAuthorizeData` on a handler
or action fail registration instead of being silently ignored. Check domain
permissions in the action and return a controlled error on denial.

## Concurrency

Up to `MaxConcurrentRequestsPerConnection` (16) actions of one connection run at the
same time, and responses can arrive out of order. When the limit is reached, DarkWS
stops reading the socket until an action finishes, so `ping`, `auth:`, and `logout`
wait as well. Concurrent actions share the connection's `HttpContext`, `Items`,
features, and `AspNetSession`, which are not thread-safe:

- Keep per-request state in scoped services, not in `HttpContext.Items`.
- Read what an action needs from `HttpContext` at its start.
- Serialize `ISession` writes yourself, or set `MaxConcurrentRequestsPerConnection = 1`.

An action starts on the connection's dispatch loop, so a synchronous handler, or
the part of an async handler before its first `await`, delays dispatch of the next
message.

## ASP.NET session state

To use `AspNetSession`, register `AddSession()` and call `UseSession()` before
`MapDarkWs()`. A WebSocket is one long request, so call
`HttpContext.Session.CommitAsync()` when a change must be persisted immediately.

## Handlers that outlive the connection

On shutdown DarkWS cancels `ConnectionAborted` and waits up to `ShutdownTimeout`.
.NET cannot stop code that ignores cancellation: such a handler keeps its scope until
it finishes, and its response is dropped. After shutdown the upgrade request is
finished and ASP.NET Core can reuse its `HttpContext`, so do not read `HttpContext`,
`AspNetSession`, or `Connection.HttpContext` from such a handler. Copy request values
you need before long work; `Session` and its `User` stay readable.
