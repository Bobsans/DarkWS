---
sidebar_position: 9
title: Troubleshooting
---

# Troubleshooting

## The upgrade fails

| Symptom | Cause and fix |
| --- | --- |
| HTTP 400 | The request is not a WebSocket upgrade. Check the client URL scheme (`ws://`/`wss://`) and any proxy in between. |
| HTTP 403 | The page's `Origin` is not in `WebSocketOptions.AllowedOrigins`. |
| HTTP 500 or another server error | The authenticator threw during the upgrade; DarkWS 4.x does not catch this exception. Check the server error log, and return `null` for invalid tokens instead of throwing. |
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
| `darkws:error:authorization-required` | The connection has no authenticated session. Authenticate first, or mark the action `[AllowAnonymous]`. In the browser, authenticate every new socket: pass the token through `query`, or call `authenticate()` on every `open`. |
| `darkws:error:invalid-request` | The request has no `action` or uses a reserved id, or the payload is missing for a non-nullable parameter or has the wrong type. Check JSON property names against the server's naming policy. |
| `darkws:error:request-failed` | The handler threw an unexpected exception. The server log has the exception. |
| Timeout with no reply | The handler is slow, or the socket is half-open. The server also sends no reply when the message is not JSON or has no string `id`, or when the handler returned `null` or a result that cannot be serialized (the server log has a warning). Check `requestTimeout` and the heartbeat settings. |

## Connections drop

| Close code or symptom | Cause and fix |
| --- | --- |
| 1009 | A message exceeded `MaxMessageSizeBytes` (server) or `MaxMessageSizeBytes` (.NET client). Send less, or raise the limit. |
| 1003 from the .NET client | The server sent a binary frame. |
| Idle connections close after about 2 minutes on .NET 8 | The client sends no traffic. The DarkWS clients ping every 30 seconds; custom clients must send `ping`. |
| The .NET client reports that the server did not acknowledge the heartbeat, then reconnects | No `pong` arrived within `PongTimeout`. A proxy may drop text frames, or the connection is at its request limit and the server has stopped reading. |
| Connections close during deployment | Shutdown cancels handlers and closes sockets within `ShutdownTimeout`. Clients reconnect automatically. |

## Broadcasts do not arrive

- The recipient's session does not list the group. Groups are read on connect and
  re-authentication; call `ConnectionStorage.Add(connection)` after changing them.
- With several instances, Redis is not configured, instances use different channel
  names, or an instance was disconnected from Redis when the message was published.
- In the browser, the `action` your `message` listener compares differs in case;
  action names are case-sensitive.
- In .NET, a typed `On<T>` subscription requires `data`; use `On(action, () => …)` for
  broadcasts without data. A broadcast with a null value also carries no `data`.

## Sessions look wrong

- `HandlerBase.Session` throws for anonymous connections. Use `Connection.Session` to
  check for null.
- `HandlerBase<TSession>.Session` throws if the session is not a `TSession`, for
  example when the default ASP.NET authenticator is still active. Register
  `AddAuthenticator<TAuthenticator, TSession>()`.
- After a failed `auth:` the session is cleared.
- After a reconnect the server session is new. The browser client restores it only
  through the upgrade `query` or another `authenticate()` call; the .NET client only
  through `AuthenticationTokenProvider`.

## Startup errors

`InvalidOperationException` at startup names the handler, method, and reason. Common
causes:

- `[Authorize]` or a policy attribute on a handler or action; see
  [Handlers and actions](server/handlers.md).
- An action that is not a public instance method, returns something other than
  `IResponse` or `Task<IResponse>`, has more than one parameter, or is generic.
- Two actions with the same `handler:action` name.
- A second `AddDarkWs()` call.

`OptionsValidationException` means an option is out of range, for example a zero
timeout.
