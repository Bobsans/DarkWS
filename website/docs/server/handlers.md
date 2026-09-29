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

Registration scans the assembly for classes with `[Handler]` and validates every
action. Invalid declarations fail at startup with an `InvalidOperationException`
that names the type, the method, and the reason.

- Call `AddDarkWs()` once per service collection; a second call throws. Reuse the
  returned `DarkWsBuilder` for more assemblies, filters, and hooks.
- Handler and action names must be non-empty and have no surrounding whitespace.
- Actions must be public instance methods declared on the scanned class. Inherited
  methods are not scanned: declare or override the action on the concrete handler
  and mark it with `[Action]`.
- An action returns exactly `IResponse` or `Task<IResponse>`.
- An action accepts zero or one payload parameter. Generic methods and
  by-reference, byref-like, or pointer parameters are rejected.
- Assembly scanning registers only handlers. Filters, initializers, and hooks are
  registered explicitly.

## Dependencies and scope

Each request runs in a new asynchronous DI scope. The handler is created from that
scope, so constructor injection of scoped services such as a `DbContext` is safe.
The scope is disposed after the response is written.

Inside an action, `HandlerBase` exposes the request context:

| Member | Meaning |
| --- | --- |
| `Connection` | The current `IWebSocketConnection` (`Id`, `Session`, `IsOpen`, `CloseAsync`, `Abort`) |
| `Session` | The authenticated session; throws for an anonymous connection |
| `HttpContext` | The upgrade request's `HttpContext`, shared by the connection |
| `AspNetSession` | ASP.NET `ISession` when session middleware is installed, otherwise null |
| `ConnectionAborted` | Cancelled when the connection stops; pass it to async work |
| `Services` | The message scope's service provider |
| `Self` | A broadcast target for the calling connection |

Derive from `HandlerBase<TSession>` to get a typed `Session`; see
[Authentication and sessions](authentication.md).

Services outside handlers can inject `IDarkWsContextAccessor` for the same data;
see [Filters and hooks](pipeline.md#context-accessor).

## Payloads

The request `data` field is deserialized into the action parameter with
`DarkWsOptions.JsonOptions` (web defaults: camelCase, case-insensitive).

- A nullable parameter (`string?`, `Input?`, `int?`) accepts a missing or null payload.
- A non-nullable parameter requires a payload. Omitted or null data returns
  `darkws:error:invalid-request` without invoking the handler.
- `AllowNullPayloads = true` passes omitted or null data to reference parameters
  even when they are annotated non-nullable; the handler must then check for null.
  Non-nullable value types still cannot receive null.
- Malformed JSON, type mismatches, and numeric overflow return
  `darkws:error:invalid-request` before the handler is constructed.
- Use `JsonElement` to accept arbitrary JSON.

## Results

Return one of the helpers:

| Helper | Wire response |
| --- | --- |
| `Ok()` | `{ "id": "…" }` |
| `Ok(value)` | `{ "id": "…", "data": value }`; `data` is written even when `value` is null |
| `Error("code")` | `{ "id": "…", "error": "code" }` |
| `Error("code", details)` | `{ "id": "…", "error": "code", "data": details }` |

Error codes must be non-empty. Use stable, namespaced codes such as
`message:not-found` that clients can switch on.

Results are serialized before the message scope is disposed, so a deferred query over
a scoped service is still enumerated while that service is alive. A `null` result, a
value that cannot be serialized (a reference cycle or unsupported type), or a custom
`IResponse` that fails before sending is answered with `darkws:error:request-failed`
and logged. The connection stays open.

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

Built-in error codes are listed in [Protocol](../protocol.md#error-codes).

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

A call to a protected action without a session returns
`darkws:error:authorization-required`.

Built-in authorization supports only this authenticated/anonymous distinction.
`[Authorize]`, role and policy attributes, and any other `IAuthorizeData` on a handler
or action fail registration instead of being silently ignored. Check domain
permissions in the action or in an [action filter](pipeline.md#action-filters), and
return a controlled error on denial.

## Concurrency

Up to `MaxConcurrentRequestsPerConnection` (16) actions of one connection run at the
same time, and responses can arrive out of order. Concurrent actions share the
connection's `HttpContext`, `Items`, features, and `AspNetSession`, which are not
thread-safe:

- Keep per-request state in scoped services, not in `HttpContext.Items`.
- Read what an action needs from `HttpContext` at its start.
- Serialize `ISession` writes yourself, or set `MaxConcurrentRequestsPerConnection = 1`.

By default an action starts on the connection's dispatch loop, so a synchronous
handler, or the part of an async handler before its first `await`, delays dispatch
of the next message. Set `RunActionsOnThreadPool = true` to schedule actions on the
thread pool instead. The concurrency limit and command ordering still apply.

## ASP.NET session state

To use `AspNetSession`, register `AddSession()` and call `UseSession()` before
`MapDarkWs()`. A WebSocket is one long request, so call
`HttpContext.Session.CommitAsync()` when a change must be persisted immediately.

## Handlers that outlive the connection

On shutdown DarkWS cancels `ConnectionAborted` and waits up to `ShutdownTimeout`.
.NET cannot stop code that ignores cancellation: such a handler keeps its scope until
it finishes, and its response is dropped. After shutdown the upgrade `HttpContext` is
recycled, so `HttpContext`, `AspNetSession`, and `Connection.HttpContext` throw
`ObjectDisposedException`. Copy request values you need before long work;
`Session` and its `User` stay readable.
