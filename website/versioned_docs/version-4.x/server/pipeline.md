---
sidebar_position: 4
title: Scope initializers and middleware
---

# Scope initializers and middleware

DarkWS has two extension points around requests and connections:

| Extension | Runs | Typical use |
| --- | --- | --- |
| Scope initializer | Before each handler invocation | Prepare scoped services (tenant, current user) |
| Middleware | On open, authentication change, and close | Presence, cleanup |

Scope initializers are resolved from the message scope, so they can inject scoped
services.

## Order of a request

1. The message is parsed. Malformed JSON is answered with `InvalidRequestError`
   when the request id can be read.
2. The action is looked up and authorization is checked; the payload is deserialized.
   Failures here are answered with `InvalidActionError`, `AuthorizationRequiredError`,
   or `InvalidRequestError`.
3. A message scope is created and **scope initializers** run in registration order.
4. The handler is resolved from the scope, runs, and the response is written.

An exception from a scope initializer or the handler is answered with
`RequestFailedError`, or with the `DarkWsException` response.

## Scope initializers

A scope initializer prepares scoped services before the handler is created:

```csharp
public sealed class TenantInitializer : IDarkWsScopeInitializer {
    public ValueTask InitializeAsync(
        IServiceProvider scopedServices, IDarkWsContextAccessor context, CancellationToken ct) {
        if (context.Session is AppSession session) {
            scopedServices.GetRequiredService<TenantContext>().AccountId = session.AccountId;
        }
        return ValueTask.CompletedTask;
    }
}

builder.Services.AddScoped<TenantContext>();
builder.Services.AddDarkWs().AddScopeInitializer<TenantInitializer>();
```

The `CancellationToken` is cancelled when the connection stops.

## Context accessor

Scoped services can inject `IDarkWsContextAccessor` to read the current request:

| Property | Meaning |
| --- | --- |
| `Connection` | The current connection |
| `Session` | The connection's current session, or null |
| `HttpContext` | The upgrade request's context |
| `AspNetSession` | ASP.NET `ISession`, or null |
| `ConnectionAborted` | Cancelled when the connection stops |

`Session` reads the connection's session at the time of the call, so an `auth:` or
`logout` processed while an action runs changes what it returns.

The accessor is initialized only inside a message scope. Reading it from an ordinary
HTTP request or from the connection scope throws `InvalidOperationException`.

## Middleware

Derive from `DarkWsMiddleware` and override what you need:

```csharp
public sealed class PresenceMiddleware(IBroadcaster broadcaster) : DarkWsMiddleware {
    public override Task OnOpenAsync(IDarkWsContextAccessor context) =>
        context.Session is AppSession session
            ? broadcaster.BroadcastToGroupAsync($"account:{session.AccountId}",
                "presence:online", new { session.UserId })
            : Task.CompletedTask;

    public override Task OnAuthenticatedAsync(IDarkWsContextAccessor context, IDarkWsSession? previousSession) =>
        Task.CompletedTask;

    public override Task OnCloseAsync(IDarkWsContextAccessor context) =>
        Task.CompletedTask;
}

builder.Services.AddDarkWs().AddHandlersFromAssemblyContaining<Program>();
```

There is no separate registration method: `AddHandlersFromAssembly` and
`AddHandlersFromAssemblyContaining<T>` register every concrete `DarkWsMiddleware`
subclass in the scanned assembly, next to the handlers. The hooks of all middleware
run one after another.

| Hook | When |
| --- | --- |
| `OnOpenAsync` | After the connection is registered |
| `OnAuthenticatedAsync` | After `auth:` replaces the session, `logout` clears it, or a failed `auth:` clears it; `previousSession` is the old one and the current one may be null |
| `OnCloseAsync` | After the connection is removed from storage and pending handlers have finished or `ShutdownTimeout` expired |

Rules for middleware:

- Use the `context` argument. An injected `IDarkWsContextAccessor` is not initialized
  in this scope.
- Middleware lives in the upgrade request's scope for the whole connection. Resolve
  short-lived dependencies through `IServiceScopeFactory`.
- An exception from `OnOpenAsync` or `OnAuthenticatedAsync` ends the connection. An
  exception from `OnCloseAsync` is logged.
- In `OnCloseAsync`, `context.ConnectionAborted` is already cancelled. DarkWS waits
  for the hook only until the `ShutdownTimeout` deadline, which it shares with pending
  handlers and the close handshake.
