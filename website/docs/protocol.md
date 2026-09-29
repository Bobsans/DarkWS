---
sidebar_position: 5
title: Protocol
---

# Protocol

This page describes the wire format, for debugging and for writing a client in
another language. The official clients implement all of it.

## Transport

- A WebSocket upgrade to the path passed to `MapDarkWs`. Browsers send an `Origin`
  header that the server may check against `AllowedOrigins`.
- An optional query parameter (`token` by default) is passed to the server's
  authenticator.
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
- `data` is omitted only when the server used an overload without data (`Ok()`,
  `PublishAsync(target, action)`). Overloads with data always write it, as `null` for
  a null value.
- Field names follow the server's `JsonOptions`; the default is camelCase.
- The `invalid-request` reply to a request with a reserved id uses an empty id, so it
  cannot be mistaken for a broadcast or a legacy control reply.

## Text commands

System commands and their replies are plain text, without JSON or ids:

| Command | Success | Failure |
| --- | --- | --- |
| `auth:<token>` | `auth:success` | `auth:failed` |
| `logout` | `logout:success` | The connection fails if logout cannot complete |
| `ping` | `pong` | None |

Because replies have no id, clients must send at most one `auth:`/`logout` at a
time and wait for its reply. If a reply does not arrive in time, the client should
close the socket, so that a late reply cannot be read as the answer to the next
command. Commands are processed in order with requests on the same connection.

Send `ping` regularly; a .NET 8 server closes connections that are silent for
`ReceiveIdleTimeout` (2 minutes). A missing `pong` tells the client the connection
is dead.

## Error codes

| Code | Meaning |
| --- | --- |
| `darkws:error:invalid-action` | No such action |
| `darkws:error:invalid-request` | Malformed JSON, missing or invalid fields, or a payload that cannot be bound |
| `darkws:error:authorization-required` | The action requires a session |
| `darkws:error:request-failed` | The handler failed unexpectedly |
| `darkws:error:busy` | The connection's request queue stayed full |
| `auth:failed` | Text reply to a rejected `auth:` command |

Servers can rename the `darkws:error:*` codes through `DarkWsOptions`. Every other
code comes from the application.

## Close and HTTP status codes

| Code | Sent by | Cause |
| --- | --- | --- |
| HTTP 400 | Server | Not a WebSocket request |
| HTTP 401 | Server | The upgrade authenticator threw |
| HTTP 403 | Server | The `Origin` is not allowed |
| 1000 | Either | Normal closure |
| 1003 | .NET client | The server sent a binary frame |
| 1008 | Server | An `auth:`/`logout` arrived when the command queue was full |
| 1009 | Either | A message exceeded the size limit |

## Client lifecycle contract

Both clients speak the same protocol but keep different lifecycle policies:

| Case | Browser `DarkWs` | .NET `DarkWsClient` |
| --- | --- | --- |
| Socket drops with reconnect disabled | Pending calls fail; no background retry. The next request or send opens a new socket. | Pending calls fail. Later calls fail until an explicit `ConnectAsync`. |
| Automatic authentication receives `auth:failed` | Queued calls reject with `ErrorResponse`. `sessionRestoreFailed` fires, then `open` signals readiness without a session; later calls go out anonymously. | Readiness fails with `DarkWsResponseException`. The state becomes `Disconnected` with the reason, and retries stop until `ConnectAsync`. |
| Token provider throws | Same failure event and anonymous fallback if the socket is still open. | Permanent readiness failure until `ConnectAsync`. |
| Token provider returns no token | Connects anonymously, without `sessionRestoreFailed`. | Permanent readiness failure. |

In the browser, `connected` and `open` report the transport and do not prove that
authentication succeeded. In .NET, `Connected` follows successful restoration.
`LazyDarkWs` follows the browser policy; its `close()` additionally lets the next use
reconnect.

Neither client keeps tokens passed to manual authentication. Reconnects use only the
application's `query` or token provider.
