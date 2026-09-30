---
sidebar_position: 6
title: Redis backplane
---

# Redis backplane

With several server instances behind a load balancer, a broadcast published on one
instance must reach clients connected to the others. `DarkWS.Redis` distributes
broadcasts through Redis Pub/Sub.

```bash
dotnet add package DarkWS.Redis --version 4.0.0
```

Register an `IConnectionMultiplexer`, then add Redis with an explicit channel name
after `AddDarkWs`:

```csharp
using DarkWS.Redis;
using StackExchange.Redis;

var redis = await ConnectionMultiplexer.ConnectAsync(builder.Configuration["Redis"]!);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<Program>();
builder.Services.AddDarkWsRedis("my-app:production");
```

- Use a unique channel per application and environment.
- The host owns the multiplexer and its lifetime; DarkWS does not dispose it.
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

- Each instance delivers Redis broadcasts one at a time: the next broadcast starts
  after the previous one has been written to its local recipients. A slow recipient
  therefore delays later broadcasts on that instance until its write completes or
  `BroadcastSendTimeout` aborts it.
- `BroadcastAsync` and the other broadcast methods complete when Redis accepts the
  message, not after delivery.
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
| `target` | `0` All, `1` Connection, `2` Session, `3` Group |
| `targetId` | Connection, session, or group id; null for All |
| `action` | Broadcast action |
| `data` | Application payload, serialized with the application's `JsonOptions` |

The numeric targets never change. Older peers that use the default envelope format
stay compatible; if older peers emitted customized envelope field names, migrate
them on a new channel.

## One subscription

The Redis backplane supports one active subscription; DarkWS subscribes when the host starts
and unsubscribes when it stops. A custom host that subscribes directly must
unsubscribe before subscribing again, since a repeated `SubscribeAsync` throws
`InvalidOperationException`. Unsubscribing cancels the token passed to a delivery in
progress.
