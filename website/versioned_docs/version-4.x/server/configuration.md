---
sidebar_position: 5
title: Configuration and limits
---

# Configuration and limits

Configure `DarkWsOptions` through `AddDarkWs`, `services.Configure<DarkWsOptions>`,
configuration binding, or `PostConfigure`; they run in the standard Options order:

```csharp
builder.Services.AddDarkWs(options => {
    options.MaxMessageSizeBytes = 256 * 1024;
    options.MaxConcurrentRequestsPerConnection = 8;
    options.ShutdownTimeout = TimeSpan.FromSeconds(5);
});

builder.Services.Configure<DarkWsOptions>(builder.Configuration.GetSection("DarkWs"));
```

Options are validated when first resolved and on host startup; invalid values throw
`OptionsValidationException`. DarkWS reads them through `IOptions<DarkWsOptions>`,
so values are fixed once they are first resolved; changing configuration at runtime
does not reconfigure DarkWS.

## Options

| Option | Default | Meaning |
| --- | --- | --- |
| `MaxMessageSizeBytes` | 1 MiB | Largest complete incoming message, all fragments included |
| `JsonOptions` | `JsonSerializerDefaults.Web` | Serialization of envelopes and payloads |
| `MaxConcurrentRequestsPerConnection` | 16 | Actions running at once on one connection |
| `KeepAliveInterval` | 30 s | Transport keep-alive interval |
| `KeepAliveTimeout` | 30 s | Transport PONG deadline (.NET 9 and later) |
| `ReceiveIdleTimeout` | 2 min | Pending read deadline (.NET 8) |
| `SendTimeout` | 30 s | Send lock wait plus socket write |
| `BroadcastSendTimeout` | 10 s | Per-recipient broadcast write deadline |
| `ShutdownTimeout` | 10 s | Handlers, middleware close hooks, and close handshake on shutdown |
| `AuthenticationQueryParameter` | `token` | Query parameter passed to the authenticator on upgrade |
| `InvalidActionError` | `darkws:error:invalid-action` | Code for unknown actions |
| `InvalidRequestError` | `darkws:error:invalid-request` | Code for malformed requests and payloads |
| `AuthorizationRequiredError` | `darkws:error:authorization-required` | Code for anonymous calls to protected actions |
| `RequestFailedError` | `darkws:error:request-failed` | Code for unexpected failures |
| `AuthenticationFailedError` | `darkws:error:authentication-failed` | Kept for source compatibility; not used, since `auth:` always replies `auth:failed` |

All timeouts must be positive and at most 4294967294 milliseconds.
`MaxMessageSizeBytes` and `MaxConcurrentRequestsPerConnection` must be positive,
`JsonOptions` must not be null, and `AuthenticationQueryParameter` and the error
codes must not be blank.

## Message size

`MaxMessageSizeBytes` is checked before JSON parsing and covers all fragments of a
message, including authentication commands. A message exactly at the limit is
accepted. A larger one closes the connection with status **1009** (Message Too Big)
without dispatching the partial message. Raise the limit only if the application
really sends large messages; memory per connection grows with it.

## Request concurrency and backpressure

Each connection runs up to `MaxConcurrentRequestsPerConnection` requests at once.
When that many are running, the connection stops reading until one of them
finishes. No request is rejected and no busy error is sent; the client's messages
wait in the socket and network buffers instead.

- Text `ping`, `auth:`, and `logout` are read by the same loop, so they wait too.
  Under sustained load a client's pong timeout can expire.
- `auth:` and `logout` are processed in the order they are read, without waiting for
  running requests.

When sizing limits, budget memory for running requests, each up to
`MaxMessageSizeBytes`, times the expected number of connections.

DarkWS bounds work inside one connection only. Enforce the total number of
connections and per-user or per-IP limits in the host or reverse proxy.

## Liveness

DarkWS detects dead peers differently per runtime:

- **.NET 9 and 10**: the server sends transport PINGs every `KeepAliveInterval` and
  aborts the connection when no PONG arrives within `KeepAliveTimeout`.
- **.NET 8**: every pending socket read is bounded by `ReceiveIdleTimeout`, reset by
  each received fragment. Idle clients must send application traffic within that
  time. The DarkWS clients send text `ping` every 30 seconds by default. Transport
  PONGs do not count. The timer runs only while a read is pending, so it is paused
  while a saturated connection stops reading.

The .NET client also detects a dead server with its own pong timeout.

## Sending and shutdown

- `SendTimeout` covers waiting for the connection's send lock and writing to the
  socket. On expiry the socket is aborted.
- Broadcasts use the shorter `BroadcastSendTimeout` per recipient, so a slow client
  cannot hold up a broadcast for long.
- On shutdown the connection leaves storage immediately and handler tokens are
  cancelled. `ShutdownTimeout` is one shared deadline for pending handlers, middleware
  close hooks, and the close handshake; if the close handshake does not finish in time,
  the socket is aborted.

## JSON

`JsonOptions` is shared by all handlers. It uses web defaults: camelCase application
DTO property names and case-insensitive reading. The envelope names `id`, `action`,
`data`, and `error` are fixed by `JsonPropertyName` attributes and do not change with
the naming policy. Clients must match the DTO schema inside `data`. The
[Redis envelope](redis.md#wire-format) uses independent serializer settings.

## Logging

- Successful actions and malformed client requests are logged at `Debug`.
- Unexpected handler failures and exceptions thrown by the authenticator during
  `auth:` are warnings.
- Handler exception messages are logged, never sent to clients.
