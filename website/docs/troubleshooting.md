---
sidebar_position: 9
title: Troubleshooting
---

# Troubleshooting

## The upgrade fails

| Symptom | Cause and fix |
| --- | --- |
| HTTP 400 | The request is not a WebSocket upgrade. Check the client URL scheme (`ws://`/`wss://`) and any proxy in between. |
| HTTP 401 | The authenticator threw. Check the server warning log. Set `AcceptAnonymousOnUpgradeAuthenticationException` to accept anonymously instead. |
| HTTP 403 | The page's `Origin` is not in `WebSocketOptions.AllowedOrigins` or `DarkWsOptions.AllowedOrigins`. |
| HTTP 404 or the upgrade never completes | `UseWebSockets()` is missing or placed after `MapDarkWs`, or a reverse proxy does not forward `Upgrade`/`Connection` headers. |
| The .NET client stops retrying after 401/403 | Authentication errors are permanent. Fix the credentials and call `ConnectAsync`. |

Behind nginx, forward the upgrade headers and raise the read timeout above the
heartbeat interval:

```nginx
location /ws {
    proxy_pass http://app;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_read_timeout 120s;
}
```

## Requests fail

| Error | Cause and fix |
| --- | --- |
| `darkws:error:invalid-action` | The name is not `handler:action`, or the handler's assembly is not registered. |
| `darkws:error:authorization-required` | The connection has no session. Authenticate first, or mark the action `[AllowAnonymous]`. In the browser, use `authenticationToken` so reconnects authenticate before requests. |
| `darkws:error:invalid-request` | The payload is missing for a non-nullable parameter, has the wrong type, or uses a reserved id. Check JSON property names against the server's naming policy. |
| `darkws:error:request-failed` | The handler threw, returned null, or returned something that cannot be serialized. The server log has the exception. |
| `darkws:error:busy` | Too many slow requests on one connection. See [backpressure](server/configuration.md#request-concurrency-and-backpressure). |
| Timeout with no reply | The handler is slow, or the socket is half-open. Check `requestTimeout` and the heartbeat settings. |

## Connections drop

| Close code or symptom | Cause and fix |
| --- | --- |
| 1009 | A message exceeded `MaxMessageSizeBytes` (server) or `MaxMessageSizeBytes` (.NET client). Send less, or raise the limit. |
| 1008 | `auth:`/`logout` commands arrived while the queue was full. Do not flood authentication. |
| 1003 from the .NET client | The server sent a binary frame. |
| Idle connections close after about 2 minutes on .NET 8 | The client sends no traffic. The DarkWS clients ping every 30 seconds; custom clients must send `ping`. |
| The browser reconnects every 30 seconds | No `pong` arrives. A proxy may drop text frames, or the server is overloaded. Check `pongTimeout`. |
| Connections close during deployment | Shutdown cancels handlers and closes sockets within `ShutdownTimeout`. Clients reconnect automatically. |

## Broadcasts do not arrive

- The recipient's session does not list the group. Groups are read on connect and
  re-authentication; call `IDarkWsConnections.Refresh(connection)` after changing them.
- The target excludes the recipient (`ExceptConnection`, `ExceptSession`).
- The group list passed to `BroadcastTarget.Groups` is empty.
- With several instances, Redis is not configured, instances use different channel
  names, or an instance was disconnected from Redis when the message was published.
- An instance older than 5.0 shares the channel and skips `Groups` messages.
- In the browser, the `onAction` name differs in case; action names are case-sensitive.
- In .NET, a typed `On<T>` subscription requires `data`; use `On(action, () => …)` for
  broadcasts without data.

## Sessions look wrong

- `HandlerBase.Session` throws for anonymous connections. Use `Connection.Session` to
  check for null.
- `HandlerBase<TSession>.Session` throws if the session is not a `TSession`, for
  example when the default ASP.NET authenticator is still active. Register
  `AddAuthenticator<TAuthenticator, TSession>()`.
- After a failed `auth:` the session is cleared unless
  `KeepSessionOnFailedAuthentication` is set.
- After a reconnect the server session is new. Clients restore it only with
  `authenticationToken` (browser) or `AuthenticationTokenProvider` (.NET).

## Startup errors

`InvalidOperationException` at startup names the handler, method, and reason. Common
causes:

- `[Authorize]` or a policy attribute on a handler or action; see
  [Authorization](server/handlers.md#authorization).
- An action that returns something other than `IResponse` or `Task<IResponse>`, has
  more than one parameter, or is generic.
- A second `AddDarkWs()`, `AddAuthenticator()`, or `AddRedis()` call.

`OptionsValidationException` means an option is out of range, for example a zero
timeout.
