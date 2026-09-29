---
sidebar_position: 6
title: Security
---

# Security

DarkWS handles framing, limits, and authentication plumbing. Token validation,
expiry, revocation, and domain permissions belong to the application. This page lists
what to configure for production.

## Checklist

- Serve the endpoint over **WSS** only.
- Restrict **origins** when you use cookies.
- Keep **tokens out of URLs**, or use short-lived tickets.
- Implement an **authenticator** that validates tokens; the default trusts only the
  HTTP identity.
- Check **domain permissions** in actions or filters.
- Enforce **session expiry and revocation** yourself.
- Limit **connections** per user or IP at the host or proxy.
- Isolate **Redis** with network rules and ACLs.
- Redact tokens and payloads in **logs**.

## Cross-site WebSocket hijacking

CORS does not apply to WebSockets. With cookie authentication, a page on another site
can open a socket to your endpoint; the browser attaches the user's cookie and the
socket becomes that user's DarkWS session.

Restrict origins:

```csharp
app.UseWebSockets(new WebSocketOptions { AllowedOrigins = { "https://app.example.com" } });
```

Other origins receive 403 before DarkWS authenticates them. Requests without an
`Origin` header (non-browser clients) are still accepted. An empty list, the
`UseWebSockets()` default, allows every origin.

If other WebSocket endpoints in the app need different origins, set
`DarkWsOptions.AllowedOrigins` instead. It applies only to the DarkWS endpoint, is
compared case-insensitively, and is checked before the authenticator runs.

## Tokens in URLs

Tokens in WebSocket URLs can be written to proxy logs, access logs, and telemetry.

- Prefer a short-lived, single-use connection ticket in the URL.
- Or, when your endpoint allows an anonymous upgrade, leave the URL clean and
  authenticate over the socket with `authenticationToken` (browser) or
  `AuthenticationTokenProvider` (.NET). Every socket then authenticates before
  protected requests are sent.
- Redact credentials in URL and message logging. See the
  [ASP.NET Core guidance on access token logging](https://learn.microsoft.com/en-us/aspnet/core/signalr/security#access-token-logging).

## Authentication attempts

DarkWS does not limit `auth:` attempts: one connection can try tokens as fast as the
network allows, and every attempt runs your authenticator.

- Use high-entropy tokens.
- When tickets are short or validation is expensive, count failures in the
  authenticator (keyed by `HttpContext.Connection.Id`, user, or client IP) and call
  `HttpContext.Abort()` to drop the connection once a limit is reached.

## Session lifetime

Token expiry or revocation does not close an existing connection. A socket can stay
open for days.

- Store the expiry in your session and check it in an action filter.
- On revocation, find the connections with `IDarkWsConnections.GetBySession(id)` and
  close them.
- `KeepSessionOnFailedAuthentication` keeps an old session after a failed refresh;
  enable it only if expiry is enforced elsewhere.
- Clients do not keep manually passed tokens. After logout, clear the application's
  token source so a reconnect cannot restore old credentials.

## Authorization

Handlers require a session unless marked `[AllowAnonymous]`. There is no built-in
role or policy authorization: `[Authorize]` fails registration so it cannot be
silently ignored. Enforce permissions in actions or [action filters](server/pipeline.md#action-filters),
and return controlled errors with `ErrorResponseException`.

## Resource limits

Per connection, DarkWS bounds message size (`MaxMessageSizeBytes`, 1 MiB),
concurrent requests (16), queued requests, and send time. See
[Configuration and limits](server/configuration.md). It does not limit:

- the number of connections: use the host (`Kestrel` limits) or a reverse proxy;
- request rate per user or IP: use rate limiting in the host or an action filter;
- Redis message size: bound what your publishers send.

## Redis

Anyone who can publish to the backplane channel can send notifications to every
client. Restrict access with network isolation and Redis ACLs. Logical database
numbers do not isolate Pub/Sub. See [Redis backplane](server/redis.md#trust-boundary).

## Error details

Unexpected exceptions are logged and answered with `darkws:error:request-failed`;
their messages never reach clients. Only data you pass to `Error(code, details)` or
`ErrorResponseException<T>` is sent.

## Reporting a vulnerability

Do not open a public issue. Report privately through
[GitHub Security Advisories](https://github.com/Bobsans/DarkWS/security/advisories/new)
with the affected package and version, the impact, and steps to reproduce. Fixes are
released for the latest major version.
