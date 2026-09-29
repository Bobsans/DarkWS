---
sidebar_position: 2
title: Authentication and sessions
---

# Authentication and sessions

A DarkWS session is an immutable object attached to a connection. It carries the
user, a session id used for [session broadcasts](broadcasts.md), and the groups the
connection belongs to. A connection without a session is anonymous and can call only
`[AllowAnonymous]` actions.

A session is set in two places:

1. **On upgrade.** When the WebSocket opens, the authenticator receives the HTTP
   request and the `token` query parameter.
2. **By command.** The client sends the text command `auth:<token>` over the open
   socket to sign in or refresh credentials, and `logout` to sign out, without
   reconnecting.

## Default: the ASP.NET identity

Without configuration, DarkWS uses the ASP.NET Core identity of the upgrade request.
If `HttpContext.User` is authenticated (cookie, bearer, or any other scheme that runs
before `MapDarkWs`), the connection gets an `AspNetDarkWsSession` with that user and
no groups. Its id is the `sid` claim, else the ASP.NET session id, else a random id
that stays stable for the connection.

This default authenticator trusts only the HTTP identity. It does not validate
`auth:<token>` commands: while the user is authenticated, any token succeeds with the
same identity. Register your own authenticator to validate tokens.

## Typed sessions

Define a session with your domain data:

```csharp
using System.Security.Claims;
using DarkWS.Abstractions;

public sealed record AppSession(
    string Id,
    ClaimsPrincipal User,
    Guid AccountId,
    Guid UserId
) : IDarkWsSession {
    public IReadOnlyCollection<string> Groups =>
        [$"account:{AccountId}", $"user:{UserId}"];
}
```

Implement `IDarkWsAuthenticator`. Return a session to accept, or null to leave the
connection anonymous:

```csharp
public sealed class AppAuthenticator(TokenValidator tokens) : IDarkWsAuthenticator {
    public async ValueTask<IDarkWsSession?> AuthenticateAsync(
        HttpContext context, string? token, CancellationToken cancellationToken) {
        if (string.IsNullOrEmpty(token)) return null;
        var ticket = await tokens.ValidateAsync(token, cancellationToken);
        if (ticket is null) return null;
        return new AppSession(ticket.SessionId, ticket.Principal, ticket.AccountId, ticket.UserId);
    }
}
```

Register both once on the DarkWS builder:

```csharp
builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<Program>()
    .AddAuthenticator<AppAuthenticator, AppSession>();
```

A second `AddAuthenticator` call throws `InvalidOperationException` before changing
any registration.

Typed handlers receive the session through `HandlerBase<TSession>` and can also
inject it into constructors and scoped services:

```csharp
[Handler("message")]
public sealed class MessageHandler(MessageService messages) : HandlerBase<AppSession> {
    [Action("send")]
    public async Task<IResponse> SendAsync(MessageInput input) {
        var result = await messages.SendAsync(Session.UserId, input);
        await PublishAsync(BroadcastTarget.Group($"account:{Session.AccountId}"), "message:created", result);
        return Ok(result);
    }
}
```

The session seen by an action is the one it was authorized with. An `auth:` or
`logout` that arrives while the action runs does not change it;
`IWebSocketConnection.Session` always shows the live value.

`HttpContext.User` follows the authenticator's decision: it is replaced on
authentication and logout, and a rejected upgrade leaves it anonymous.

## Authenticator lifetime

- The upgrade authenticator is resolved from the upgrade request's scope, which
  lives for the whole connection.
- Each `auth:` command resolves the authenticator from a new scope.

Resolve short-lived dependencies such as a `DbContext` through `IServiceScopeFactory`
if the authenticator is used at upgrade.

## Upgrade authentication

The upgrade reads the token from the `token` query parameter; change it with
`DarkWsOptions.AuthenticationQueryParameter`. The token is optional: without one,
your authenticator decides from the HTTP request alone.

| Authenticator result | Outcome |
| --- | --- |
| A session | Connection opens authenticated |
| `null` | Connection opens anonymous |
| Throws | Logged as a warning; HTTP 401, no socket |

Set `AcceptAnonymousOnUpgradeAuthenticationException = true` to accept the socket
anonymously when the authenticator throws. Request and shutdown cancellation is never
treated as an authentication failure.

Tokens in URLs can reach proxy and access logs. Prefer a short-lived connection
ticket, or open the socket anonymously and authenticate with `auth:` (see
[Security](../security.md#tokens-in-urls)).

## Authentication commands

| Command | Success reply | Failure reply |
| --- | --- | --- |
| `auth:<token>` | `auth:success` | `auth:failed` |
| `logout` | `logout:success` | The connection fails if logout cannot complete |

- A successful `auth:` replaces the session. `logout` always clears it.
- By default a failed `auth:` (rejection, empty token, or exception) also clears the
  previous session. Set `KeepSessionOnFailedAuthentication = true` to keep the
  current session, principal, and group indexes; the reply is still `auth:failed`.
  Use it only when the host still enforces the retained session's expiry.
- Commands keep their order relative to requests on the connection.
- Already running actions are not rolled back by logout.

Both clients send these commands for you: `authenticate()`/`logout()` in the browser
and `AuthenticateAsync`/`LogoutAsync` in .NET. They also can restore the session on
every reconnect; see the [browser](../clients/browser.md#authentication) and
[.NET](../clients/dotnet.md#authentication) client pages.

## React to session changes

`DarkWsConnectionHooks.OnAuthenticatedAsync` runs after the session is replaced,
cleared by logout, or cleared by a failed `auth:`. It receives the previous session;
the current one may be null. It is not called when
`KeepSessionOnFailedAuthentication` keeps the session unchanged. See
[Filters and hooks](pipeline.md#connection-hooks).

## Changing groups

Group membership is read from `IDarkWsSession.Groups` when the connection is added
and after each re-authentication. If your application changes membership another
way, call `Refresh(connection)` on the injected `IDarkWsConnections`; see
[Broadcasts](broadcasts.md#connections-and-groups).

## Responsibilities of the application

DarkWS does not validate, expire, or revoke tokens:

- Token expiry or revocation does not close an existing connection. Enforce session
  lifetime and ongoing authorization in the application, for example by closing
  connections from a revocation handler.
- DarkWS does not limit `auth:` attempts; see
  [Security](../security.md#authentication-attempts).
