---
sidebar_position: 3
title: Broadcasts
---

# Broadcasts

A broadcast is a server-initiated message with an action name and optional data.
Clients receive it as `{ "id": "@", "action": "…", "data": … }`.

Broadcast from a handler with the `Broadcast*Async` methods of `HandlerBase`, or from
any service by injecting `IBroadcaster`:

```csharp
public sealed class OrderNotifier(IBroadcaster broadcaster) {
    public Task OrderShippedAsync(Order order, CancellationToken ct) =>
        broadcaster.BroadcastToGroupAsync(
            $"account:{order.AccountId}",
            "order:shipped",
            new { order.Id, order.TrackingNumber },
            ct);
}
```

The overloads without data send no `data` field. The overloads with data also omit
`data` when the value is null.

## Targets

The method selects the recipients:

| Method | Recipients |
| --- | --- |
| `BroadcastAsync(action, …)` | Every connection |
| `BroadcastToConnectionAsync(connectionId, action, …)` (`IBroadcaster` only) | One connection |
| `BroadcastToSessionAsync(sessionId, action, …)` | Every connection of one session, such as all tabs of a user |
| `BroadcastToGroupAsync(group, action, …)` | Connections whose session lists the group |
| `BroadcastToSelfAsync(action, …)` (handlers only) | The calling connection |

Every method has an overload without data and a generic overload with data, and
takes an optional `CancellationToken`. Each broadcast has exactly one target; to reach
several groups, broadcast to each group.

## Delivery semantics

- **Selection** uses one snapshot of the group and session indexes.
- **Serialization** happens once per broadcast; all recipients share the same bytes.
- **Local delivery** writes to the recipients in parallel, a limited number at a time.
  A socket that does not accept the write within `BroadcastSendTimeout` (10 seconds)
  is aborted.
- **Order** between broadcasts published concurrently is not guaranteed. Include a
  version or sequence number in the data when order matters.
- **Cancellation**: with the in-memory backplane the publisher's token is also passed
  to local delivery. Cancelling it while a broadcast is delivered skips the remaining
  recipients and cancels writes in progress, which aborts those sockets. The Redis
  backplane checks the token only before publishing.
- **Completion**: with the in-memory backplane the broadcast method completes after
  local delivery; with Redis it completes when Redis accepts the message.
- **No replay**: a client that is disconnected misses the broadcast. Treat
  broadcasts as change notifications and have clients reload state after reconnecting.

## Connections and groups

Inject the singleton `ConnectionStorage` to inspect local connections:

```csharp
public sealed class PresenceService(ConnectionStorage connections) {
    public int OnlineInAccount(Guid accountId) =>
        connections.GetByGroup($"account:{accountId}").Count;
}
```

| Member | Purpose |
| --- | --- |
| `GetAll()` | Every connection on this instance |
| `GetByConnection(id)` | The connection with this id, or an empty collection |
| `GetBySession(id)` | Connections of one session |
| `GetByGroup(name)` | Members of one group |
| `Add(connection)` | Adds or replaces a connection by id and re-reads its session groups into the indexes |
| `Remove(connection)` | Removes this exact connection; returns false when it is absent or was replaced |

DarkWS adds and removes connections itself. Lookups return snapshots.

Groups are snapshotted when a connection is added and after each `auth:` or `logout`.
If your session exposes groups that change independently, call `Add(connection)`
again to refresh the indexes. `Add` does not check whether the connection is still
open: re-adding a connection that has already been removed registers it again.

These lookups see only the current instance. With Redis, broadcasts still reach
connections on every instance.

## Backplanes

The broadcaster hands every message to an `IDarkWsBackplane`, which delivers it to
each server instance. The default in-memory backplane serves one instance.
For several instances, use the [Redis backplane](redis.md). Handler code does not
change when the backplane changes.

A custom backplane implements `PublishAsync(DarkWsBroadcast)`, `SubscribeAsync(listener)`,
and `UnsubscribeAsync()`. DarkWS subscribes when the host starts and unsubscribes when
it stops. Register the backplane as a singleton `IDarkWsBackplane` with
`services.Replace(...)`, or with `AddSingleton` before calling `AddDarkWs()`.
