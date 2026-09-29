---
sidebar_position: 6
title: Redis backplane
---

# Redis backplane

With several server instances behind a load balancer, a broadcast published on one
instance must reach clients connected to the others. `DarkWS.Redis` distributes
broadcasts through Redis Pub/Sub.

```bash
dotnet add package DarkWS.Redis
```

Register an `IConnectionMultiplexer` and add Redis with an explicit channel name:

```csharp
using DarkWS.Redis;
using StackExchange.Redis;

var redis = await ConnectionMultiplexer.ConnectAsync(builder.Configuration["Redis"]!);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<Program>()
    .AddRedis("my-app:production");
```

- Use a unique channel per application and environment.
- Call `AddRedis` once; a second call throws `InvalidOperationException` instead of
  silently switching channels.
- The host owns the multiplexer and its lifetime.
- Handler code does not change.

The package requires StackExchange.Redis 2.13.17 or later and is tested against
2.13.17 and 3.2.1.

## Delivery guarantees

Redis Pub/Sub delivers **at most once**. Broadcasts published while an instance is
disconnected from Redis (restart, failover, network loss) never reach that
instance's clients, and nothing reports the gap.

Treat broadcasts as change notifications, not as the record of state. After
`IConnectionMultiplexer.ConnectionRestored`, as after a client reconnect, have
clients reload from the source of truth.

## Throughput and ordering

- Each instance runs up to 16 deliveries concurrently, so a slow recipient does not
  hold up unrelated broadcasts.
- Writes to one socket stay serialized, but broadcast order on a connection is not
  guaranteed. Include a version or sequence number when order matters.
- `PublishAsync` completes when Redis accepts the message, not after delivery.
- Pending messages wait in StackExchange.Redis's unbounded subscription queue.
  Sustained publishing above delivery throughput grows memory. Limit publisher rate
  and payload size.

## Trust boundary

Anyone who can publish to the channel can send arbitrary notifications to clients on
every instance, including targeted sessions and groups.

- Isolate Redis by network access and restrict the channels with ACLs.
- Unique channel names separate environments but do not authorize anything.
- Logical database numbers do **not** isolate Pub/Sub channels
  ([Redis documentation](https://redis.io/docs/latest/develop/pubsub/#database--scoping)).
- `MaxMessageSizeBytes` limits WebSocket input, not Redis messages. Bound the data
  your publishers produce.

## Wire format

The Redis envelope uses fixed JSON settings, independent of the application's
`JsonOptions`:

| Field | Meaning |
| --- | --- |
| `target` | `0` All, `1` Connection, `2` Session, `3` Group, `4` Groups |
| `targetId` | Connection, session, or group id; null for All and Groups |
| `action` | Broadcast action |
| `data` | Application payload, serialized with the application's `JsonOptions` |
| `groups` | Group names (Groups only) |
| `except` | Optional `connectionId` and `sessionId` exclusions (Groups only) |

The numeric targets never change.

Group unions and **every** target with an exclusion use `target: 4`, even for a
single group. Instances older than 5.0 reject this target and skip delivery rather
than ignore the exclusion. Upgrade every server sharing a channel before using
`Groups`, `ExceptConnection`, or `ExceptSession`, or roll out on a new channel.
Messages without exclusions keep the older single-target format.

## One subscription

A backplane supports one active subscription; DarkWS manages it for you. A custom
host that subscribes directly must unsubscribe before subscribing again, since a
repeated `SubscribeAsync` throws `InvalidOperationException`.
