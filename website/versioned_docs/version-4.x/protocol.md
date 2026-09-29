---
sidebar_position: 5
title: Protocol
---

# Protocol

This page describes the wire format, for debugging and for writing a client in
another language. The official clients implement all of it.

## Transport

- A WebSocket upgrade to the path passed to `MapDarkWs`. Browsers send an `Origin`
  header; DarkWS does not check it, but ASP.NET Core's
  `WebSocketOptions.AllowedOrigins` can (see [Security](security.md)).
- An optional query parameter (`token` by default, `AuthenticationQueryParameter`)
  is passed to the server's authenticator. When the authenticator returns no
  session, the connection is accepted anonymously. When it throws, the exception
  escapes the endpoint and the upgrade fails with a server error.
- **Text frames only.** The server currently treats a binary frame as text with the
  same bytes, and the .NET client closes the socket with 1003; do not rely on binary
  frames.
- Messages larger than the server's `MaxMessageSizeBytes` (1 MiB) close the
  connection with 1009.

## JSON messages

| Direction | Shape |
| --- | --- |
| Request | `{ "id": string, "action": string, "data"?: unknown }` |
| Success | `{ "id": string, "data"?: unknown }` |
| Error | `{ "id": string, "error": string, "data"?: unknown }` |
| Broadcast | `{ "id": "@", "action": string, "data"?: unknown }` |

```json
→ { "id": "7f3c", "action": "math:sum", "data": { "left": 2, "right": 3 } }
← { "id": "7f3c", "data": { "value": 5 } }

→ { "id": "7f3d", "action": "message:delete", "data": { "id": "42" } }
← { "id": "7f3d", "error": "message:forbidden" }

← { "id": "@", "action": "message:created", "data": { "text": "hi" } }
```

- The client chooses request ids. They must be non-empty and must not be `@` or
  `@auth`; a request with a reserved id is rejected without running its action.
- Responses carry the request's id and may arrive in any order.
- A response is an error whenever it has an `error` field.
- In responses, `data` is omitted only when the server used an overload without data
  (`Ok()`, `Error(code)`). `Ok(value)` and `Error(code, details)` always write it, as
  `null` for a null value.
- In broadcasts, `data` is omitted when the server broadcast without data or with a
  null value.
- The envelope field names are fixed. Field names inside `data` follow the server's
  `JsonOptions`; the default is camelCase.
- The `invalid-request` reply to a request with a reserved id uses an empty id, so it
  cannot be mistaken for a broadcast or a control reply. A message that is not JSON,
  or has no readable string `id`, gets no reply at all.

## Text commands

System commands and their replies are plain text, without JSON or ids:

| Command | Success | Failure |
| --- | --- | --- |
| `auth:<token>` | `auth:success` | `auth:failed` |
| `logout` | `logout:success` | The connection fails if logout cannot complete |
| `ping` | `pong` | None |

`auth:` with an empty token, a rejected token, or an authenticator that throws all
reply `auth:failed` and clear the connection's previous session. `logout` also clears
the session. Requests that are already running are not rolled back.

Because replies have no id, clients must send at most one `auth:`/`logout` at a
time and wait for its reply. If a reply does not arrive in time, the client should
close the socket, so that a late reply cannot be read as the answer to the next
command. The server reads one message at a time and finishes a command before it
reads the next message.

Send `ping` regularly; a .NET 8 server closes connections that are silent for
`ReceiveIdleTimeout` (2 minutes). The .NET client treats a missing `pong` as a dead
connection; the browser client sends `ping` but does not check the reply. While a
connection is at its request limit, the server stops reading, so `pong` can be
delayed by slow handlers.

## Error codes

| Code | Meaning |
| --- | --- |
| `darkws:error:invalid-action` | No such action |
| `darkws:error:invalid-request` | Missing or invalid envelope fields, a reserved id, or a payload that cannot be bound |
| `darkws:error:authorization-required` | The action requires an authenticated session |
| `darkws:error:request-failed` | The handler failed unexpectedly |
| `auth:failed` | Text reply to a rejected `auth:` command |

Servers can rename the `darkws:error:*` codes through `DarkWsOptions`. Every other
code comes from the application.

## Close and HTTP status codes

| Code | Sent by | Cause |
| --- | --- | --- |
| HTTP 400 | Server | Not a WebSocket request |
| HTTP 403 | ASP.NET Core | The `Origin` is not in `WebSocketOptions.AllowedOrigins` |
| Server error (usually HTTP 500) | ASP.NET Core | The upgrade authenticator threw |
| 1000 | Either | Normal closure |
| 1003 | .NET client | The server sent a binary frame |
| 1009 | Server or .NET client | A message exceeded the size limit |

## Client lifecycle contract

Both clients speak the same protocol but keep different lifecycle policies:

| Case | Browser `DarkWs` | .NET `DarkWsClient` |
| --- | --- | --- |
| Socket drops with reconnect disabled | Pending calls fail; no background retry. The next request or send opens a new socket. | Pending calls fail. Later calls fail until an explicit `ConnectAsync`. |
| Session on a new socket | No automatic authentication. The upgrade `query` is built again for every socket; otherwise call `authenticate()` after each `open`. | `AuthenticationTokenProvider`, when set, authenticates every socket before requests are sent. |
| Automatic authentication fails (`auth:failed`, the provider throws, or it returns no token) | Not applicable. | Readiness fails permanently: the state becomes `Disconnected` with the reason, and retries stop until `ConnectAsync`. |

In the browser, `connected` and `open` report the transport and do not prove that
authentication succeeded. In .NET, `Connected` follows successful automatic
authentication.

Neither client keeps tokens passed to manual authentication. Reconnects use only the
application's `query` (browser) or token provider (.NET).
