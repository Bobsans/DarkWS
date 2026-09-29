# DarkWS.Redis

Redis backplane for `DarkWS`.

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddDarkWsRedis("my-app:production");
```

Register `DarkWS` before this package. Use a unique channel per
application and environment. Call `AddDarkWsRedis` once; a second call throws
`InvalidOperationException`.

Redis Pub/Sub delivers at most once: broadcasts published while an instance is
disconnected from Redis are lost for that instance's clients without an error.
Treat broadcasts as change notifications and have clients refresh state after
`IConnectionMultiplexer.ConnectionRestored`, as after a client reconnect.

Each subscription runs up to 16 broadcast deliveries concurrently. A slow
recipient therefore does not hold up an unrelated broadcast while another
delivery slot is available. Sends to each socket remain serialized, but broadcast
order on a connection is not guaranteed; include a version/sequence in application
data or refresh authoritative state when ordering matters.

Only active deliveries are bounded. Pending messages remain in StackExchange.Redis's
unbounded subscription queue; sustained publication above delivery throughput can
still grow memory. Limit publisher rate and payload size. DarkWS does not add a new
overflow/drop policy or durable delivery. The load regression uses 900 broadcasts
with 32 KiB data, a 10 ms publication interval, and a stuck recipient with a 100 ms
send deadline; it checks pending work and retained managed memory across three windows.

The host owns the connection multiplexer. This package retains its 2.13.17
minimum dependency and is also tested against StackExchange.Redis 3.2.1.

The Redis wire envelope uses immutable JSON settings, independent of application
`DarkWsOptions.JsonOptions`. Fields are `target`, `targetId`, `action`, and `data`.
Numeric targets are fixed: All=0, Connection=1, Session=2, Group=3, Groups=4. Payload data is
already JSON and preserves the application's serialization choices. Default-format
older peers remain compatible; coordinate a channel migration for older peers
that emitted customized envelope names.

Groups=4 adds `groups` and optional `except` with `connectionId` and/or `sessionId`.
It carries group unions and single-group exclusions in one Redis publication.
Each server deduplicates local recipients and applies exclusions to its indexed
membership snapshot. Invalid group selections are rejected during deserialization.
Upgrade every server and Redis package sharing a channel before using these
methods: old nodes reject Groups=4 and skip delivery. This prevents old nodes from
silently ignoring exclusions. Legacy target messages retain their previous format.

Only one Redis subscription can be active. A repeated Subscribe call throws
`InvalidOperationException`; call Unsubscribe before replacing it. Cancelling the
subscription token or unsubscribing cancels the token supplied to active listeners.
Cancelled deliveries are skipped; call Unsubscribe to remove the Redis subscription.
Receiver tasks are tracked and the lifetime source is released after they finish.
Unsubscribe does not wait for an uncooperative listener, and can be called from a
listener without waiting for itself. Already-running listeners must honor cancellation.

Static callers should use `DarkWsRedisServiceCollectionExtensions`. The old
`RedisConfiguration` static wrapper is obsolete; extension-call syntax is unchanged.

## Trust boundary

Anyone who can publish to this channel can send arbitrary notifications to clients
on every instance, including targeted sessions/groups. Use network isolation and
Redis ACLs that restrict publication/subscription to the application's channels.
Unique names separate environments but are not an authorization mechanism.
Logical database numbers do not isolate Pub/Sub; use channel ACLs or separate
Redis instances for stronger boundaries ([Redis documentation](https://redis.io/docs/latest/develop/pubsub/#database--scoping)).

Backplane messages are trusted and do not use `MaxMessageSizeBytes`; that setting
only limits incoming WebSocket messages. Bound data produced by trusted publishers.
