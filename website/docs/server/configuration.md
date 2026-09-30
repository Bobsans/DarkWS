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
    options.AllowedOrigins.Add("https://app.example.com");
});

builder.Services.Configure<DarkWsOptions>(builder.Configuration.GetSection("DarkWs"));
```

Options are validated when first resolved and on host startup; invalid values throw
`OptionsValidationException`. Connections and singleton services capture the options
they use, so changing options at runtime does not reconfigure existing connections.

## Options

| Option | Default | Meaning |
| --- | --- | --- |
| `MaxMessageSizeBytes` | 1 MiB | Largest complete incoming message, all fragments included |
| `JsonOptions` | `JsonSerializerDefaults.Web` | Serialization of envelopes and payloads |
| `AllowNullPayloads` | `false` | Pass null to non-nullable reference parameters |
| `MaxConcurrentRequestsPerConnection` | 16 | Actions running at once on one connection |
| `RequestQueueTimeout` | 5 s | Wait for queue space before `BusyError` |
| `RunActionsOnThreadPool` | `false` | Start actions on the thread pool |
| `KeepAliveInterval` | 30 s | Transport keep-alive interval |
| `KeepAliveTimeout` | 30 s | Transport PONG deadline (.NET 9 and later) |
| `ReceiveIdleTimeout` | 2 min | Pending read deadline (.NET 8) |
| `SendTimeout` | 30 s | Send lock wait plus socket write |
| `BroadcastSendTimeout` | 10 s | Per-recipient broadcast write deadline |
| `ShutdownTimeout` | 10 s | Handlers, close hooks, and close handshake on shutdown |
| `AllowedOrigins` | empty (any) | Browser origins allowed to open the endpoint |
| `AuthenticationQueryParameter` | `token` | Query parameter passed to the authenticator on upgrade |
| `AcceptAnonymousOnUpgradeAuthenticationException` | `false` | Accept anonymously when the upgrade authenticator throws |
| `KeepSessionOnFailedAuthentication` | `false` | Keep the session after a failed `auth:` |
| `InvalidActionError` | `darkws:error:invalid-action` | Code for unknown actions |
| `InvalidRequestError` | `darkws:error:invalid-request` | Code for malformed requests and payloads |
| `AuthorizationRequiredError` | `darkws:error:authorization-required` | Code for anonymous calls to protected actions |
| `RequestFailedError` | `darkws:error:request-failed` | Code for unexpected failures |
| `BusyError` | `darkws:error:busy` | Code for requests rejected by a full queue |

All timeouts must be positive and at most 4294967294 milliseconds.

## Message size

`MaxMessageSizeBytes` is checked before JSON parsing and covers all fragments of a
message, including authentication commands. A message exactly at the limit is
accepted. A larger one closes the connection with status **1009** (Message Too Big)
without dispatching the partial message. Raise the limit only if the application
really sends large messages; memory per connection grows with it.

## Request concurrency and backpressure

Each connection has its own queue:

- Up to `MaxConcurrentRequestsPerConnection` requests run at once (1 to
  `int.MaxValue - 4`).
- As many more ordinary requests wait in FIFO order.
- Four extra places are reserved for `auth:` and `logout`, which keep their order
  relative to requests.
- A separate intake holds at most `MaxConcurrentRequestsPerConnection + 4` messages
  while one admission worker waits for dispatcher space. It preserves request and
  command arrival order.
- Transport PONGs keep being read under load. Text `ping` replies use a separate
  worker with one pending ping slot; repeated pings are coalesced while a write is busy.

An ordinary request waiting for dispatcher space longer than `RequestQueueTimeout`
is answered with `darkws:error:busy`. Socket reads continue during that wait and
during reply writes. A full intake, or a command admitted to a full dispatcher
queue, closes the connection with status **1008** (Policy Violation).

Clients should limit in-flight work and pace batches by replies to avoid overflowing
the intake. `RequestQueueTimeout` need not be shorter than `KeepAliveTimeout`.
Budget for running requests, both bounded queues, one message being admitted, and
one being received, with each message up to `MaxMessageSizeBytes`, per connection.

DarkWS bounds work inside one connection only. Enforce the total number of
connections and per-user or per-IP limits in the host or reverse proxy.

## Liveness

DarkWS detects dead peers differently per runtime:

- **.NET 9 and 10**: the server sends transport PINGs every `KeepAliveInterval` and
  aborts the connection when no PONG arrives within `KeepAliveTimeout`.
- **.NET 8**: every pending socket read is bounded by `ReceiveIdleTimeout`, reset by
  each received fragment. Idle clients must send application traffic within that
  time. The DarkWS clients send text `ping` every 30 seconds by default. Transport
  PONGs do not count. Admission waits do not pause the receive timer.

Both clients also detect dead servers with their own ping/pong timeout.

## Sending and shutdown

- `SendTimeout` covers waiting for the connection's send lock and writing to the
  socket. On expiry the socket is aborted.
- Broadcasts use the shorter `BroadcastSendTimeout` per recipient, so a slow client
  cannot hold up others.
- On shutdown the connection leaves storage immediately. Peer close cancels action,
  authentication, authentication-hook, and command-write tokens. `ShutdownTimeout`
  is one shared deadline for pending actions and commands, close hooks, and the
  close handshake; after it the socket is aborted.
- Callbacks that ignore cancellation keep their scope and deferred connection
  resources until they finish. Shutdown still completes within its deadline;
  late authentication results and replies are discarded. Copy required HTTP
  context values before such work because the upgrade request can already be over.
- The accepted socket is also closed and disposed if initial session registration
  fails, including exceptions from the session's `Id` or `Groups`.

## JSON

`JsonOptions` is shared by all handlers. It uses web defaults: camelCase application
DTO property names and case-insensitive reading. The envelope names `id`, `action`,
`data`, and `error` are fixed by `JsonPropertyName` attributes and do not change with
the naming policy. Clients must match the DTO schema inside `data`. The
[Redis envelope](redis.md#wire-format) uses independent serializer settings.

## Logging

- Successful actions and malformed client requests are logged at `Debug`.
- Unexpected handler failures and upgrade authentication failures are warnings.
- Handler exception messages are logged, never sent to clients.
