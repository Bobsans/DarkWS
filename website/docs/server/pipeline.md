---
sidebar_position: 4
title: Filters and hooks
---

# Filters and hooks

DarkWS has four extension points around requests and connections:

| Extension | Runs | Typical use |
| --- | --- | --- |
| Request filter | Around every well-formed request, including rejected ones | Metrics, tracing, logging |
| Scope initializer | Before each handler invocation | Prepare scoped services (tenant, current user) |
| Action filter | Around the bound handler | Permission checks, validation, error mapping |
| Connection hooks | On open, authentication change, and close | Presence, cleanup |

Filters and initializers are resolved from the message scope, so they can inject
scoped services.

## Order of a request

1. The message is parsed. Malformed JSON and busy rejections are answered here,
   before any scope exists.
2. A message scope is created and **request filters** start, outermost first.
3. The action is looked up and authorization is checked; the payload is bound.
   Failures here produce an `ErrorResponse` seen by request filters.
4. **Scope initializers** run in registration order.
5. **Action filters** run, outermost first, around the handler.
6. The handler runs and the response is written.

## Request filters

```csharp
public sealed class MetricsFilter(ILogger<MetricsFilter> logger) : IDarkWsRequestFilter {
    public async ValueTask<IResponse> InvokeAsync(
        DarkWsRequestContext context, Func<ValueTask<IResponse>> next) {
        var started = Stopwatch.GetTimestamp();
        try {
            var response = await next();
            var outcome = response is ErrorResponse error ? error.Error : "ok";
            logger.LogInformation("{Action} {Outcome} in {Elapsed}",
                context.ActionName, outcome, Stopwatch.GetElapsedTime(started));
            return response;
        } catch (Exception exception) {
            logger.LogWarning(exception, "{Action} failed", context.ActionName);
            throw;
        }
    }
}

builder.Services.AddDarkWs().AddRequestFilter<MetricsFilter>();
```

Request filters see:

- unknown actions (`context.Action` is null), missing authorization, and invalid
  payloads as an `ErrorResponse` whose `Error` is the configured code;
- a failing scope initializer, action filter, or handler as the exception. DarkWS then
  answers with `RequestFailedError`, or with the `DarkWsException` response.

`DarkWsRequestContext` carries `RequestId`, the client's `ActionName`, the registered
`Action` (or null), `RawPayload`, `Session`, `Services`, and `CancellationToken`.

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

`context.Action` describes the action being invoked.

## Action filters

Action filters wrap the handler after lookup, authorization, binding, and
initializers. They run in registration order, the first one outermost:

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireRoleAttribute(string role) : Attribute {
    public string Role { get; } = role;
}

public sealed class RoleFilter : IDarkWsActionFilter {
    public ValueTask<IResponse> InvokeAsync(
        DarkWsActionContext context, Func<ValueTask<IResponse>> next) {
        var required = context.Action.Attributes
            .Concat(context.Action.HandlerAttributes)
            .OfType<RequireRoleAttribute>();
        foreach (var attribute in required) {
            if (context.Session?.User.IsInRole(attribute.Role) != true) {
                return ValueTask.FromResult<IResponse>(new ErrorResponse("auth:forbidden"));
            }
        }
        return next();
    }
}

builder.Services.AddDarkWs().AddActionFilter<RoleFilter>();
```

A filter may return its own `IResponse` without calling `next`, or inspect the
response or catch the exception from `next`. Call `next` at most once.

`DarkWsActionContext` exposes:

| Property | Meaning |
| --- | --- |
| `Action` | `DarkWsActionInfo`: `Name`, `HandlerType`, `Method`, method `Attributes`, class `HandlerAttributes` |
| `RequestId` | The client's request id |
| `Payload` / `RawPayload` | The deserialized parameter and the received JSON |
| `Session` | The session captured for this request |
| `Services` | The message scope's service provider |
| `CancellationToken` | Cancelled when the connection stops |

Custom attributes such as `RequireRole` above are the supported way to add
declarative authorization, since `[Authorize]` fails registration.

## Context accessor

Scoped services can inject `IDarkWsContextAccessor` to read the current request:

| Property | Meaning |
| --- | --- |
| `Connection` | The current connection |
| `Session` | The session the action was authorized with |
| `HttpContext` | The upgrade request's context |
| `AspNetSession` | ASP.NET `ISession`, or null |
| `ConnectionAborted` | Cancelled when the connection stops |
| `Action` | The action being invoked; null in connection hooks |

The accessor is initialized only inside a message scope. Reading it from an ordinary
HTTP request or from the connection scope throws `InvalidOperationException`.

## Connection hooks

Derive from `DarkWsConnectionHooks` and override what you need:

```csharp
public sealed class PresenceHooks(IBroadcaster broadcaster) : DarkWsConnectionHooks {
    public override Task OnOpenAsync(IDarkWsContextAccessor context) =>
        context.Session is AppSession session
            ? broadcaster.PublishAsync(BroadcastTarget.Group($"account:{session.AccountId}"),
                "presence:online", new { session.UserId })
            : Task.CompletedTask;

    public override Task OnAuthenticatedAsync(IDarkWsContextAccessor context, IDarkWsSession? previousSession) =>
        Task.CompletedTask;

    public override Task OnCloseAsync(IDarkWsContextAccessor context) =>
        Task.CompletedTask;
}

builder.Services.AddDarkWs().AddConnectionHooks<PresenceHooks>();
```

| Hook | When |
| --- | --- |
| `OnOpenAsync` | After the connection is registered |
| `OnAuthenticatedAsync` | After `auth:` replaces the session, `logout` clears it, or a failed `auth:` clears it; `previousSession` is the old one and the current one may be null |
| `OnCloseAsync` | After the connection is removed from storage |

Rules for hooks:

- Use the `context` argument. An injected `IDarkWsContextAccessor` is not initialized
  in this scope.
- Hooks live in the upgrade request's scope for the whole connection. Resolve
  short-lived dependencies through `IServiceScopeFactory`.
- In `OnCloseAsync`, `context.ConnectionAborted` is the `ShutdownTimeout` deadline
  shared with the close handshake, so async cleanup can run until it expires.
