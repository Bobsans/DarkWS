---
sidebar_position: 3
title: Broadcasts
---

# Broadcasts

A broadcast is a server-initiated message with an action name and optional data.
Clients receive it as `{ "id": "@", "action": "…", "data": … }`.

Publish from a handler with `PublishAsync`, or from any service by injecting
`IBroadcaster`:

```csharp
public sealed class OrderNotifier(IBroadcaster broadcaster) {
    public Task OrderShippedAsync(Order order, CancellationToken ct) =>
        broadcaster.PublishAsync(
            BroadcastTarget.Group($"account:{order.AccountId}"),
            "order:shipped",
            new { order.Id, order.TrackingNumber },
            ct);
}
```

`PublishAsync(target, action)` sends no `data` field. The overloads with data always
write it, as JSON `null` for a null value, regardless of `JsonOptions.DefaultIgnoreCondition`.

## Targets

`BroadcastTarget` selects the recipients:

| Target | Recipients |
| --- | --- |
| `BroadcastTarget.All` | Every connection |
| `BroadcastTarget.Connection(id)` | One connection |
| `BroadcastTarget.Session(id)` | Every connection of one session, such as all tabs of a user |
| `BroadcastTarget.Group(name)` | Connections whose session lists the group |
| `BroadcastTarget.Groups(names)` | The union of several groups, once per connection |
| `Self` (in handlers) | The calling connection |

Group targets can exclude recipients:

```csharp
await PublishAsync(
    BroadcastTarget.Groups(["account:42", "editors"]).ExceptConnection(Connection.Id),
    "document:changed", new { DocumentId = 7 });

await PublishAsync(
    BroadcastTarget.Group("account:42").ExceptSession(Session.Id),
    "document:changed");
```

- `ExceptConnection` skips one connection, typically the caller.
- `ExceptSession` skips every connection of a session.
- When both are set, a connection matching either is skipped.
- `All`, `Connection`, and `Session` do not take exclusions.

`Groups` reads its sequence once and removes duplicates. Its group snapshot is
immutable, including through collection casts, and stays stable in `Except` copies.
Changing the original list does not change the target. An empty sequence publishes
nothing, and unknown groups have no recipients. Null collections and blank ids,
group names, or action names are rejected with an argument exception. An already
cancelled token throws before publishing.

## Delivery semantics

- **Selection** uses one snapshot of the group and session indexes.
- **Serialization** happens once per broadcast; all recipients share the same bytes.
- **Local delivery** writes to all recipients concurrently. A socket that does not
  accept the write within `BroadcastSendTimeout` (10 seconds) is aborted without
  delaying the others.
- **Order** on one connection is not guaranteed. Include a version or sequence number
  in the data when order matters.
- **Cancellation** of the publisher's token prevents publishing but does not stop a
  delivery that has started.
- **Completion**: with the in-memory backplane `PublishAsync` completes after local
  delivery; with Redis it completes when Redis accepts the message.
- **No replay**: a client that is disconnected misses the broadcast. Treat
  broadcasts as change notifications and have clients reload state after reconnecting.

## Connections and groups

Inject `IDarkWsConnections` to inspect local connections:

```csharp
public sealed class PresenceService(IDarkWsConnections connections) {
    public int OnlineInAccount(Guid accountId) =>
        connections.GetByGroup($"account:{accountId}").Count;
}
```

| Member | Purpose |
| --- | --- |
| `Find(id)` | One connection, or null |
| `GetAll()` | Every connection on this instance |
| `GetBySession(id)` | Connections of one session |
| `GetByGroup(name)` | Members of one group |
| `Refresh(connection)` | Re-reads the connection's session groups into the indexes |

Groups are snapshotted when a connection is added and after each re-authentication.
If your session exposes groups that change independently, call `Refresh(connection)`
afterwards. It returns false for a closed or unregistered connection, so a refresh
that races with disconnect cannot register it again.

These lookups see only the current instance. With Redis, broadcasts still reach
connections on every instance.

## Backplanes

The broadcaster hands every message to an `IDarkWsBackplane`, which delivers it to
each server instance. The default in-memory backplane serves one instance.
For several instances, use the [Redis backplane](redis.md). Handler code does not
change when the backplane changes.

A custom backplane implements `PublishAsync(BroadcastMessage)`, `SubscribeAsync(listener)`,
and `UnsubscribeAsync()`. Register it as a singleton `IDarkWsBackplane` with
`services.Replace(...)`, or with `AddSingleton` before calling `AddDarkWs()`.

If you construct a `BroadcastMessage` directly, keep its caller-supplied group list
unchanged while the message is in use; that low-level DTO does not copy the list.
