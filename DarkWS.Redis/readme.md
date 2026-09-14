# DarkWS.Redis

Redis backplane for `DarkWS`.

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddDarkWsRedis("my-app:production");
```

Register `DarkWS` before this package. Use a unique channel per
application and environment.

The host owns the connection multiplexer. This package retains its 2.13.17
minimum dependency and is also tested against StackExchange.Redis 3.2.1.

The Redis wire envelope uses immutable JSON settings, independent of application
`DarkWsOptions.JsonOptions`. Fields are `target`, `targetId`, `action`, and `data`.
Numeric targets are fixed: All=0, Connection=1, Session=2, Group=3. Payload data is
already JSON and preserves the application's serialization choices. Default-format
older peers remain compatible; coordinate a channel migration for older peers
that emitted customized envelope names.

Only one Redis subscription can be active. A repeated Subscribe call throws
`InvalidOperationException`; call Unsubscribe before replacing it. Cancelling the
subscription token or unsubscribing cancels the token supplied to active listeners.

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
