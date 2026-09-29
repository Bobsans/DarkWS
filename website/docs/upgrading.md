---
sidebar_position: 8
title: Upgrading to 5.0
---

# Upgrading to 5.0

All packages share one version. Upgrade the server, Redis instances, and clients
together. The full list of changes is in the
[changelog](https://github.com/Bobsans/DarkWS/blob/main/CHANGELOG.md).

## Server registration

| 4.x | 5.0 |
| --- | --- |
| `services.AddDarkWsRedis(channel)` | `services.AddDarkWs().AddRedis(channel)` |
| `Configuration.AddDarkWs(services)` | `services.AddDarkWs()` |
| `Configuration.MapDarkWs(endpoints)` | `endpoints.MapDarkWs(pattern)` |
| `RedisConfiguration.AddDarkWsRedis(...)` | `services.AddDarkWs().AddRedis(channel)` |
| Subclass of `DarkWsMiddleware` | Subclass of `DarkWsConnectionHooks`, registered with `AddConnectionHooks<T>()` |
| Repeated `AddAuthenticator` calls | One call on the shared builder; a second call throws |
| `options.AuthenticationFailedError = …` | Remove; failed text authentication always replies `auth:failed` |

Connection hooks are no longer found by assembly scanning; register each explicitly.

## Broadcasts

`PublishAsync` with a `BroadcastTarget` replaces the old broadcast methods on
`IBroadcaster` and `HandlerBase`:

| 4.x | 5.0 |
| --- | --- |
| `BroadcastAsync(action, data)` | `PublishAsync(BroadcastTarget.All, action, data)` |
| `BroadcastToConnectionAsync(id, action, data)` | `PublishAsync(BroadcastTarget.Connection(id), action, data)` |
| `BroadcastToSessionAsync(id, action, data)` | `PublishAsync(BroadcastTarget.Session(id), action, data)` |
| `BroadcastToGroupAsync(group, action, data)` | `PublishAsync(BroadcastTarget.Group(group), action, data)` |
| `BroadcastToSelfAsync(action, data)` | `PublishAsync(Self, action, data)` |

Pass a cancellation token positionally after the data. Custom `IBroadcaster`
implementations implement the two `PublishAsync` overloads that take a token.

Custom backplanes and tests rename `DarkWsBroadcast` to `BroadcastMessage`,
`DarkWsTarget` to `BroadcastTargetType`, and the envelope's `Target`/`Groups` to
`TargetType`/`GroupNames`, then recompile. The Redis wire format is unchanged, so 4.x
and 5.0 nodes still exchange single-target messages. Before using `Groups` or
exclusions, upgrade every node on the channel (see
[Redis wire format](server/redis.md#wire-format)).

Redis deliveries now run concurrently: do not rely on broadcast order on one
connection.

## Connections

`ConnectionStorage` is internal. Inject `IDarkWsConnections`:

| 4.x | 5.0 |
| --- | --- |
| `storage.Add(connection)` after changing groups | `connections.Refresh(connection)` |
| `storage.GetByConnection(id)` | `connections.Find(id)` |
| Fake recipients in tests | `DarkWsTestHost.CreateConnection` from `DarkWS.Testing` |

Custom `IWebSocketConnection` implementations remove `WebSocket` and
`ReceiveMessageAsync`, implement `Abort()`, and take `ReadOnlyMemory<byte>` in
`SendAsync`.

The wire envelope records (`InputMessage`, `OkMessage`, `ErrorMessage`, and others)
are internal. Code that serialized them should use its own records with the
documented `id`, `action`, `data`, and `error` fields.

## Behavior changes

- **`HttpContext` after shutdown.** Handlers that keep running after
  `ConnectionAborted` and then read `HttpContext`, `Items`, headers, or
  `AspNetSession` get `ObjectDisposedException`. Copy the values first. `Session` is
  unaffected.
- **Busy connections.** A connection that pipelines many slow requests can receive
  `darkws:error:busy` after `RequestQueueTimeout` (5 s). Retry, or raise
  `MaxConcurrentRequestsPerConnection` or `RequestQueueTimeout`, keeping the timeout
  below keep-alive and client pong timeouts.
- **Command flooding.** Four queue places are reserved for `auth:`/`logout`; a command
  that finds the queue full closes the connection with 1008.
- **Upgrade authentication errors** return HTTP 401 instead of escaping as server
  errors. Opt into `AcceptAnonymousOnUpgradeAuthenticationException` to accept
  anonymously instead.

## Browser client

- Authentication and logout use `controlTimeout` (30 s) instead of `requestTimeout`.
  Set `controlTimeout` to your old `requestTimeout` to keep the previous deadline.
- `authenticate("")` rejects with `TypeError`; call `logout()` instead.
- A socket that misses `pong` for 30 seconds is closed and reconnected. Set
  `pongTimeout: 0` for the old behavior, and rename `pingTimeout` to `pingInterval`.
- `connect()` no longer replaces an open socket; call `close()` and then `connect()`.
- More than 256 simultaneous calls reject with `RangeError`. Raise
  `maxPendingRequests` if you need more.

## New in 5.0

- Group unions and exclusions: `BroadcastTarget.Groups`, `ExceptConnection`,
  `ExceptSession`.
- [Action filters, request filters](server/pipeline.md), and `DarkWsActionInfo` metadata.
- `DarkWS.Testing` for [testing handlers](server/testing.md) without sockets.
- `AllowedOrigins`, `RunActionsOnThreadPool`, `AllowNullPayloads`,
  `KeepSessionOnFailedAuthentication`, and `AcceptAnonymousOnUpgradeAuthenticationException`.
- Browser: `authenticationToken`, `sessionRestoreFailed`, retries, `requestOptions`,
  `onAction`, `DarkWs.lazy()`, `isCurrentSocket`, and `reconnectOnVisible`.
