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
    .AddRedis("my-app:production", options => {
        options.QueueCapacity = 256;
        options.MaxMessageSizeBytes = 1024 * 1024;
    });
```

- Use a unique channel per application and environment.
- Call `AddRedis` once; a second call throws `InvalidOperationException` instead of
  silently switching channels.
- The host owns the multiplexer and its lifetime.
- Handler code does not change.
- The configuration callback is optional; these are the defaults. Limits must be
  positive and are copied at registration.

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
- A Redis callback feeds a bounded local queue without waiting for delivery. It
  holds at most `QueueCapacity` messages; when full, the **new** message is dropped.
  There is no replay or disconnect on overflow, and other publishers remain independent.
- Serialized envelopes larger than `MaxMessageSizeBytes` are rejected by
  `PublishAsync` with `ArgumentException` before publishing. Oversize values sent by
  another publisher are dropped before deserialization.
- Queued wire payload is bounded by `QueueCapacity * MaxMessageSizeBytes`; up to 16
  more messages can be in active delivery. This is not a process memory limit:
  publisher serialization, dependency/network buffers, and application allocations
  are outside that queue. Also limit publication rate in the host.

The `DarkWS.Redis` meter exposes counters tagged with `channel`:

| Instrument | Meaning |
| --- | --- |
| `darkws.redis.received` | Messages observed by an active subscription, including dropped ones |
| `darkws.redis.dropped` | Incoming messages discarded, tagged with `reason=capacity` or `reason=oversize` |
| `darkws.redis.rejected` | Local publications rejected for exceeding the envelope size limit |

Observe these with `MeterListener` or an OpenTelemetry metrics exporter. A successful
publish still means only that Redis accepted the message; it cannot report a
subscriber's overflow. Size the limits and reload application state when gaps matter.

## Trust boundary

Anyone who can publish to the channel can send arbitrary notifications to clients on
every instance, including targeted sessions and groups.

- Isolate Redis by network access and restrict the channels with ACLs.
- Unique channel names separate environments but do not authorize anything.
- Logical database numbers do **not** isolate Pub/Sub channels
  ([Redis documentation](https://redis.io/docs/latest/develop/pubsub/#database--scoping)).
- `DarkWsOptions.MaxMessageSizeBytes` limits WebSocket input. The independent
  `DarkWsRedisOptions.MaxMessageSizeBytes` limits the complete Redis envelope.

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
