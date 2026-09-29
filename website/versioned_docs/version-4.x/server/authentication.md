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
no groups. Its id is the `sid` claim, else the ASP.NET session id, else a new random
id.

This default authenticator trusts only the HTTP identity. It does not validate
`auth:<token>` commands: while `HttpContext.User` is authenticated, any non-blank
token succeeds with the same identity. After `logout` or a failed `auth:`,
`HttpContext.User` is anonymous, so every later `auth:` on that connection fails.
Register your own authenticator to validate tokens.

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

Protected actions check `User.Identity.IsAuthenticated`, so give the session a
principal with an authenticated identity (a `ClaimsIdentity` created with an
authentication type).

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

Call `AddAuthenticator` once. A repeated call replaces the authenticator, while the
typed session registrations of earlier calls remain.

Typed handlers receive the session through `HandlerBase<TSession>` and can also
inject it into constructors and scoped services:

```csharp
[Handler("message")]
public sealed class MessageHandler(MessageService messages) : HandlerBase<AppSession> {
    [Action("send")]
    public async Task<IResponse> SendAsync(MessageInput input) {
        var result = await messages.SendAsync(Session.UserId, input);
        await BroadcastToGroupAsync($"account:{Session.AccountId}", "message:created", result);
        return Ok(result);
    }
}
```

`HandlerBase.Session` and `IDarkWsContextAccessor.Session` read the connection's
current session on every access. An `auth:` or `logout` processed while an action
runs changes what the action sees afterwards; after `logout`, `HandlerBase.Session`
throws `InvalidOperationException`.
Read the session once at the start of an action that must use one identity.

`HttpContext.User` is set to the session's user when the upgrade authenticator
returns a session, and is left unchanged when it returns null. Every `auth:` and
`logout` replaces it: with the new session's user, or with an anonymous principal
when the session is cleared.

## Authenticator lifetime

The authenticator is resolved from the upgrade request's scope, which lives for the
whole connection. The same instance handles the upgrade and every `auth:` command
on that connection.

Resolve short-lived dependencies such as a `DbContext` through `IServiceScopeFactory`.

## Upgrade authentication

The upgrade reads the token from the `token` query parameter; change it with
`DarkWsOptions.AuthenticationQueryParameter`. The token is optional: without one,
your authenticator decides from the HTTP request alone.

| Authenticator result | Outcome |
| --- | --- |
| A session | Connection opens authenticated |
| `null` | Connection opens anonymous |
| Throws | The exception escapes the endpoint; no socket is opened |

Tokens in URLs can reach proxy and access logs. Prefer a short-lived connection
ticket, or open the socket anonymously and authenticate with `auth:` (see
[Security](../security.md)).

## Authentication commands

| Command | Success reply | Failure reply |
| --- | --- | --- |
| `auth:<token>` | `auth:success` | `auth:failed` |
| `logout` | `logout:success` | The connection fails if logout cannot complete |

- A successful `auth:` replaces the session. `logout` always clears it.
- A failed `auth:` (a null result, a blank token, or an exception, which is logged
  as a warning) also clears the previous session. The reply is always
  `auth:failed`; `DarkWsOptions.AuthenticationFailedError` does not change it.
- Commands are handled in the connection's read loop: messages that follow a
  command are dispatched after it completes.
- Already running actions are not rolled back by logout.

Both clients send these commands for you: `authenticate()`/`logout()` in the browser
and `AuthenticateAsync`/`LogoutAsync` in .NET. The .NET client can also restore the
session on every new socket with `AuthenticationTokenProvider`; the browser client
re-reads its `query` option on every connection. See the
[browser](../clients/browser.md) and [.NET](../clients/dotnet.md) client pages.

## React to session changes

`DarkWsMiddleware.OnAuthenticatedAsync` runs after every `auth:` and `logout`: when
the session is replaced, cleared by logout, or cleared by a failed `auth:`. It
receives the previous session; the current one may be null. An exception thrown
from it closes the connection. See [Middleware](pipeline.md#middleware).

## Changing groups

Group membership is read from `IDarkWsSession.Groups` when the connection is added
and after each `auth:` and `logout`. If your application changes membership another
way, call `Add(connection)` on the injected `ConnectionStorage` to refresh the
indexes; see [Broadcasts](broadcasts.md).

## Responsibilities of the application

DarkWS does not validate, expire, or revoke tokens:

- Token expiry or revocation does not close an existing connection. Enforce session
  lifetime and ongoing authorization in the application, for example by closing
  connections from a revocation handler.
- DarkWS does not limit `auth:` attempts; see [Security](../security.md).
