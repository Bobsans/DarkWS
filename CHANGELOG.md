# Changelog

All notable changes are recorded here using versioned entries and the categories
Added, Changed, Deprecated, Removed, Fixed, and Security. Behavior changes must
include migration guidance before a release is published.

## [Unreleased]

### Added

- DWA-07: Browser `maxPendingRequests` (default 256) bounds application requests and
  manual authentication/logout, including connection waits and retry delays. Excess
  calls reject immediately with `RangeError` without adding requests or timers.
  Automatic session restoration can still run when the application budget is full.
- DWA-06: Browser `sessionRestoreFailed(error, event)` reports rejected automatic
  authentication or a token-provider error before anonymous-ready `open`. The event
  identifies the socket and lets applications close it to stop that fallback.
  Existing browser and .NET lifecycle policies remain unchanged; the protocol docs
  compare them and a shared integration scenario exercises both clients.
- Browser client `DarkWs.lazy(options | () => options)` returns a `LazyDarkWs`
  facade that creates its client on first use and connects it on the first
  request, command, or subscription. It keeps subscriptions across `reset()`,
  which disposes the client and re-reads the options, restarting it if it was
  started; `reset(true)` also drops subscriptions. `close()` lets the next use
  connect again.
- Browser client option `requestOptions(action)` supplies default request options
  per action. Explicit request options override the defaults field by field; it
  is not used for authentication or logout.
- Browser client `onAction<TData>(action, callback)` filters one exact broadcast
  action, passes its data directly, and returns an idempotent unsubscribe function.
  It reuses the existing message subscriptions and survives reconnects.
- Browser request options `{ timeout, retry: { connectionClosed, timeout, jitter } }`
  with independent retry budgets and random delay in milliseconds. Retries are
  disabled by default and must only be enabled for idempotent actions. Each attempt
  uses a fresh id; explicit close/disposal cancels pending retries. Numeric request
  timeouts remain supported. Exported `DarkWsRequestOptions` and `DarkWsRetryOptions`.
- `ConnectionClosedError.sent` reports whether any attempt of the request was
  accepted by the socket, including when the final attempt was unsent. This does
  not acknowledge server execution.
- Browser client `controlTimeout` independently limits authentication/logout
  replies, including automatic session restore. Default 30 seconds; `0` disables
  the deadline, which starts when each command is sent.
- Browser client `isCurrentSocket(event)` identifies native events from the
  currently assigned socket without changing event payloads. Opt-in
  `reconnectOnVisible` skips pending reconnect backoff when the tab becomes
  visible, respecting connection gates and explicit close/disposal.
- `DarkWsOptions.RunActionsOnThreadPool` (default false) moves action processing
  to the thread pool so synchronous handlers do not stall the dispatcher.
  Scheduled actions retain the per-connection concurrency limit and capture their
  DarkWS session before scheduling; queue backpressure is unchanged.
- `DarkWsOptions.AllowNullPayloads` (default false) permits omitted/null data for
  reference action parameters regardless of their nullable annotations. Both sync
  and async handlers receive null directly. Non-nullable value types and malformed
  non-null payloads retain their existing validation.
- `DarkWsOptions.AcceptAnonymousOnUpgradeAuthenticationException` (default false)
  and `KeepSessionOnFailedAuthentication` (default false). Failed `auth:` commands
  can retain the current session, principal, and indexes without a lifecycle hook;
  the response stays `auth:failed`, and explicit logout always clears the session.
- `PublishAsync(BroadcastTarget, action[, data][, cancellationToken])` on
  `IBroadcaster` and `HandlerBase`. `BroadcastTarget` selects `All`, a
  `Connection`, `Session`, `Group`, or a `Groups` union published once with
  deduplicated recipients; group targets take `ExceptConnection`/`ExceptSession`.
  New recipient options extend the target instead of adding broadcaster overloads.
  `HandlerBase.Self` selects the calling connection.
- Action filters (#8): `DarkWsBuilder.AddActionFilter<TFilter>()` registers scoped
  `IDarkWsActionFilter` implementations that run in registration order around the
  bound handler; a filter may return its own `IResponse` or catch the handler's
  exception. `DarkWsActionContext` carries the action metadata, `RequestId`, the
  deserialized `Payload` and the received `RawPayload`, the captured `Session`, the
  message `Services`, and the `CancellationToken`.
- Request filters: `DarkWsBuilder.AddRequestFilter<TFilter>()` registers scoped
  `IDarkWsRequestFilter` implementations that wrap every well-formed request,
  including unknown actions, missing authorization, and invalid payloads (seen as
  `ErrorResponse` results) and failing scope initializers (seen as exceptions).
  `DarkWsRequestContext` carries `RequestId`, the client's `ActionName`, the
  registered `Action` or null, `RawPayload`, `Session`, `Services`, and the token.
  Malformed JSON and busy rejections do not reach request filters.
- `DarkWsActionInfo` describes a registered action: `Name`, `HandlerType`, `Method`,
  method `Attributes`, and `HandlerAttributes` from the handler class, both collected
  once at registration. `IDarkWsContextAccessor.Action` exposes it to scope
  initializers and scoped services, and is null in connection lifecycle hooks.
- `HandlerBase.Services` is the current message scope's service provider.
- `ErrorResponse.Error` and `ErrorResponse<T>.Error` expose the error code, so filters
  can classify outcomes.
- `DarkWS.Testing`: direct handler context initialization, captured responses and
  targeted broadcasts, and registered action invocation through the server pipeline
  without sockets. `HandlerBase` no longer depends on the connection dispatcher;
  existing core public interfaces are unchanged.
- AUD-03: `DarkWsOptions.RequestQueueTimeout` (default 5 seconds) and
  `DarkWsOptions.BusyError` (default `darkws:error:busy`).
- AUD-04: Browser client option `authenticationToken`. It runs when each socket
  opens; the returned token is sent as `auth:<token>`, and queued and new requests
  are held until `auth:success`, so requests made during reconnect no longer reach
  the server before authentication. `open` fires after that exchange; `auth:failed`
  or a provider error rejects the held requests and keeps the connection without a
  session. Without the option, behavior is unchanged.
- AUD-13: `IWebSocketConnection.Abort()` with a default implementation.
- AUD-19: Browser client options `pingInterval` (replacing the misnamed
  `pingTimeout`, which stays as a deprecated alias) and `pongTimeout` (default
  30 seconds, `0` disables it).
- AUD-28: An integration test runs the browser package, compiled from source, on
  Node.js's WebSocket against a real DarkWS server.
- AUD-38: The npm package also ships `src`, so its source maps resolve, and its
  `exports` gain a `default` condition for resolvers that do not use `import`.

### Changed

- Local broadcast delivery shares one `BroadcastSendTimeout` deadline per message
  instead of a linked token source and timer per recipient. Every send still starts
  at once and times out after the same interval. In the in-memory fanout benchmark
  (.NET 10, 1000 recipients) allocation per publication drops from about 156 KB to
  12 KB and time from 0.47 ms to 0.14 ms.
- Breaking: `IWebSocketConnection` keeps only the members applications and test
  doubles use: `Id`, `HttpContext`, `Session`, `IsOpen`, `SendAsync`, `CloseAsync`,
  `Abort`, and `Dispose`. `SendAsync` takes `ReadOnlyMemory<byte>`; `WebSocket` and
  `ReceiveMessageAsync` are removed, and `Abort` has no default implementation.
- Breaking: `DarkWsMiddleware` is renamed `DarkWsConnectionHooks` and is registered
  with `DarkWsBuilder.AddConnectionHooks<T>()`. Assembly scanning registers only handlers.
- Breaking: Redis is registered with `DarkWsBuilder.AddRedis(channel)` from
  `DarkWsRedisBuilderExtensions`, replacing `services.AddDarkWsRedis(channel)`.
- Breaking: `PublishAsync` replaces `BroadcastAsync`, `BroadcastToConnectionAsync`,
  `BroadcastToSessionAsync`, and `BroadcastToGroupAsync` on `IBroadcaster` and
  `HandlerBase`, and `HandlerBase.BroadcastToSelfAsync`; the old methods are removed.
  `PublishAsync` rejects a blank action and an already cancelled token before
  publishing, for every target. Wire envelopes are unchanged.
- Breaking: the backplane envelope `DarkWsBroadcast` is renamed `BroadcastMessage` and its
  selector enum `DarkWsTarget` is renamed `BroadcastTargetType`, including in
  `IDarkWsBackplane` and `DarkWsTestHost.Broadcasts`. The envelope's `Target` and
  `Groups` properties are renamed `TargetType` and `GroupNames` to match
  `BroadcastTarget` (`Type`, `Id`, `GroupNames`, `Except`). Enum values, JSON field
  names, and the Redis wire format are unchanged. This requires the next major version.
- DWA-04: A repeated `AddAuthenticator` call throws `InvalidOperationException`
  before changing service registrations, including repeated calls with the same types.
- DWA-02: Redis subscriptions run up to 16 concurrent broadcast deliveries, so one
  slow recipient no longer serializes the stream. Broadcast order on a connection
  is not guaranteed. Receiver tasks are tracked and cancelled on unsubscribe;
  lifetime resources are released after completion. Pending Redis queue capacity
  is unchanged, and no new overflow/drop policy is introduced.
- A command arriving at a full shared connection queue closes the socket with
  status 1008 (Policy Violation), instead of indefinitely blocking reads.
  `MaxConcurrentRequestsPerConnection` must leave room for four reserved command
  places and is now limited to `int.MaxValue - 4`.
- Browser client `authenticate("")` rejects immediately with `TypeError` without
  connecting, sending, or joining the control-command queue. Use `logout()` to
  sign out.
- Authenticator exceptions during upgrade are logged and return HTTP 401 instead
  of escaping as server errors. Request/shutdown cancellation is propagated.
- AUD-03: The server keeps reading a saturated connection. Up to
  `MaxConcurrentRequestsPerConnection` requests run and as many more wait in arrival
  order together with `auth:`/`logout`; text `ping` and transport PONGs are handled
  at once, so keep-alive and client heartbeats no longer drop healthy connections
  under load. When the queue stays full for `RequestQueueTimeout`, the next request
  is answered with `BusyError` and reading continues, instead of reads pausing
  until a slot frees; commands still wait for a place.
- AUD-36: The Redis backplane publishes and reads its envelope as UTF-8 bytes
  without an intermediate string. The bytes on the channel are unchanged.
- AUD-40: A second `AddDarkWsRedis` call throws `InvalidOperationException`
  instead of silently switching to the last channel.
- AUD-45: The published package list lives once, as `DarkWsPackages` in
  `Directory.Build.props`; package settings and the gate scripts read it.

### Removed

- DWA-05: `DarkWsOptions.AuthenticationFailedError`, unused since 4.0.
- The obsolete static wrappers `Configuration` (`AddDarkWs`, `MapDarkWs`) and
  `RedisConfiguration` (`AddDarkWsRedis`).
- Public wire envelope records `InputMessage`, `OkMessage`, `ResponseMessage<T>`,
  `ErrorMessage`, `ErrorMessage<T>`, `BroadcastActionMessage`, and
  `BroadcastActionMessage<T>`, and the constant `DarkWsProtocol.LegacyAuthenticationId`,
  are now internal. The JSON protocol and the reserved `@auth` request id are unchanged.
- `WebSocketConnection`, `ReceivedMessage`, and `ConnectionStorage` are now internal.
  The injected `IDarkWsConnections` replaces `ConnectionStorage` for lookups by id,
  session, or group and for `Refresh(connection)`; applications no longer register
  or remove connections directly.

### Migration

- Handlers that keep running after `ConnectionAborted` and then read `HttpContext`,
  `Items`, request headers, or `AspNetSession` must copy those values first; the
  context now throws `ObjectDisposedException` after shutdown. `Session` is unaffected.
- Replace `ConnectionStorage` with the injected `IDarkWsConnections`. Call
  `Refresh(connection)` instead of `Add(connection)` after changing group membership,
  and `Find(id)` instead of `GetByConnection(id)`. To test with fake recipients, use
  `DarkWsTestHost.CreateConnection` from `DarkWS.Testing`.
- Rename `DarkWsMiddleware` subclasses to derive from `DarkWsConnectionHooks` and
  register each with `AddConnectionHooks<T>()`; they are no longer found by
  `AddHandlersFromAssembly`.
- Replace `services.AddDarkWsRedis(channel)` with `services.AddDarkWs().AddRedis(channel)`.
- Custom `IWebSocketConnection` implementations remove `WebSocket` and
  `ReceiveMessageAsync`, implement `Abort()`, and take `ReadOnlyMemory<byte>` in
  `SendAsync`. Pass `new byte[] { ... }` or an array instead of a `[...]` literal.

- Replace `BroadcastAsync(action, ...)` with `PublishAsync(BroadcastTarget.All, action, ...)`,
  `BroadcastToConnectionAsync(id, ...)`, `BroadcastToSessionAsync(id, ...)`, and
  `BroadcastToGroupAsync(group, ...)` with `PublishAsync(BroadcastTarget.Connection(id), ...)`,
  `.Session(id)`, and `.Group(group)`, and `BroadcastToSelfAsync(...)` with
  `PublishAsync(Self, ...)`. Pass a cancellation token positionally after the data,
  or use the token-only overload. Custom `IBroadcaster` implementations implement the
  two `PublishAsync` overloads with a cancellation token instead of the eight old methods.
- Replace `DarkWsBroadcast` with `BroadcastMessage`, `DarkWsTarget` with
  `BroadcastTargetType`, and the envelope's `Target`/`Groups` with `TargetType`/`GroupNames` in
  custom backplanes and tests, then recompile: assemblies built against 4.x fail to
  load these types. Nodes on 4.x and the new version still exchange Redis messages.
- Browser applications with more than 256 simultaneous request/control calls must
  wait for capacity or explicitly increase `maxPendingRequests`. The value must be
  a positive safe integer; retry attempts retain the original call's slot.
- Register the authenticator and typed session once through the shared DarkWS builder;
  remove repeated `AddAuthenticator` calls from application modules. Previously the
  last authenticator won while earlier typed-session factories remained registered.
- Remove `AuthenticationFailedError` assignments and configuration entries. The option
  is unused; failed text authentication always replies `auth:failed`.
- Replace `Configuration.AddDarkWs(services)`, `Configuration.MapDarkWs(endpoints)`, and
  `RedisConfiguration.AddDarkWsRedis(services, channel)` with the extension calls
  `services.AddDarkWs()`, `endpoints.MapDarkWs()`, and `services.AddDarkWs().AddRedis(channel)`.
- Code that serialized or parsed the wire envelope types should use its own records
  with the documented `id`, `action`, `data`, and `error` fields.
- Redis consumers must not rely on publication order on one connection. Include
  application versions/sequences or refresh authoritative state when ordering
  matters. Bound publication rate and payload size: the 16-delivery limit does not
  impose a hard memory cap on StackExchange.Redis's pending subscription queue.
- Avoid flooding a connection with `auth:`/`logout`: the shared FIFO now reserves
  four extra command places and closes with 1008 when a command cannot enter it.
  Ordinary requests retain their existing queue limit and `RequestQueueTimeout`.
  Commands still execute after earlier requests; the reserve keeps the reader
  available for heartbeat traffic rather than prioritizing authentication.
- Browser control commands now use `controlTimeout` (30 seconds) rather than
  `requestTimeout` (5 minutes by default). Set `controlTimeout` to the old
  `requestTimeout` value to preserve the previous deadline. Replace
  `authenticate("")` calls with `logout()`; empty automatic token-provider results
  still skip authentication.
- To accept anonymous connections after upgrade authenticator exceptions, opt into
  `AcceptAnonymousOnUpgradeAuthenticationException`. Normal null results still allow
  anonymous upgrade. Enable `KeepSessionOnFailedAuthentication` only when the host
  continues to enforce the retained session's expiry and revocation.
- Upgrade all server and Redis packages sharing a channel before using group unions
  or exclusions. Their Groups=4 envelope adds `groups` and `except`; old nodes reject
  it. Existing single-target messages, including `PublishAsync` to a target without
  exclusions, are unchanged.
- AUD-03: Clients that pipeline many slow requests on one connection can now
  receive `darkws:error:busy`. Retry such requests, or raise
  `MaxConcurrentRequestsPerConnection` or `RequestQueueTimeout`, keeping the
  timeout below `KeepAliveTimeout` and the clients' pong timeouts.
- AUD-19: The browser client now closes a socket that misses `pong` for 30 seconds.
  Set `pongTimeout: 0` to keep the previous behavior, and rename `pingTimeout` to
  `pingInterval`.
- AUD-21: Code that called `connect()` to replace an open browser socket must call
  `close()` and then `connect()`.
- AUD-40: Call `AddDarkWsRedis` once per service collection; previously the last
  call's channel won.

### Security

- AUD-05: The quick starts configure `WebSocketOptions.AllowedOrigins` and explain
  cross-site WebSocket hijacking with cookie authentication. A test covers that a
  foreign `Origin` receives 403 before DarkWS authenticates the upgrade. The
  `UseWebSockets()` default still allows every origin.
- AUD-10: The release workflow runs dependency installation, tests, and packing in
  a `verify` job without `id-token: write`. Only the `publish` job in the `release`
  environment holds that permission; it publishes the verified artifacts and the
  npm tarball without installing dependencies or running package scripts.
- AUD-29: Workflow actions are pinned to commit SHAs with version comments, and
  Dependabot keeps them current.
- AUD-30: The release publishes exactly the packages the gate packed, inspected,
  and installed (`DARKWS_PACKAGE_OUTPUT`), instead of packing them again.
- AUD-39: Document that `auth:` attempts on a connection are not limited and how
  an authenticator can bound them.
- AUD-44: `SECURITY.md` describes private vulnerability reporting. Dependabot also
  proposes NuGet and npm updates, except for the published libraries' deliberate
  minimum dependency versions.
- DWA-16 (#27): `DarkWsOptions.AllowedOrigins` restricts the DarkWS endpoint to listed
  browser origins, compared case-insensitively, independently of
  `WebSocketOptions.AllowedOrigins`. Other origins get 403 before the authenticator
  runs; requests without `Origin` are accepted. The default empty list allows every origin.
- DWA-11 (#30): CI and release checkouts no longer persist `GITHUB_TOKEN` in
  `.git/config` while npm, NuGet restore, and tests run.

### Fixed

- #40: Redis subscriptions now use a bounded callback-fed queue (default 256) with
  drop-newest overflow and observable received/dropped/rejected counters. The
  upcoming 5.0 release also defaults Redis envelopes to 1 MiB: oversize local
  publications throw and inbound values are dropped. Configure both limits with
  `AddRedis(channel, options => ...)`, rate-limit publishers, and reload state after
  gaps; the 16-worker limit and at-most-once, unordered delivery remain unchanged.
- #34: Request admission and reply writes no longer stall transport reads under
  saturation. `RequestQueueTimeout` and FIFO command order are preserved by a
  separate bounded intake. In the upcoming 5.0 release, peers that overflow that
  intake close with status 1008; clients should pace request batches by replies.
  Pending text pings are coalesced while a PONG write is busy.
- DWA-18 (#25): A .NET client `LogoutAsync` rejected by `MaxPendingRequests` no longer
  disables the authentication token provider; only a started logout does.
- DWA-08 (#24): When .NET client subscribers keep failing and an `Error` observer is
  slow, at most `NotificationQueueCapacity` `Error` events wait and newer ones are
  dropped, instead of queueing without bound. `StateChanged` events are not dropped.
- DWA-09 (#26): After a connection has shut down, a handler that ignored cancellation
  gets `ObjectDisposedException` from `HttpContext`, `AspNetSession`, and
  `IWebSocketConnection.HttpContext` instead of reading the recycled context of a
  finished request.
- DWA-10, DWA-12 (#29): The package gate reads the npm tarball with .NET instead of
  `tar`, which failed under Git for Windows, and tests the same Release build it packs.
- DWA-14 (#31): The browser contract test fails instead of being skipped when the
  Node.js tooling is missing and `CI=true`.

- AUD-01: A request whose result cannot be serialized (reference cycle, unsupported
  type), whose action returns `null`, or whose custom `IResponse` fails before
  sending now receives `darkws:error:request-failed` with its id instead of no reply.
  Results are serialized before the message scope is disposed, so deferred data over
  scoped services (for example an unmaterialized EF Core query) is returned instead
  of failing. A scope disposal failure after the reply was sent is logged and no
  longer replaces the result.
- AUD-02: In the .NET client, a socket drop or acknowledgement timeout while
  automatic authentication waits for `auth:success` is a transport failure that
  reconnects instead of stopping the client. The pending authentication fails as
  soon as the socket stops, not at `ConnectionTimeout`, and a drop during the token
  provider call cancels the provider's token. `auth:failed`, a failing or empty token
  provider, HTTP 401/403, and wire errors still stop retries; a provider that exceeds
  `ConnectionTimeout` is retried.
- AUD-06: Broadcast delivery writes to all local recipients concurrently instead
  of `Environment.ProcessorCount` at a time, so slow sockets no longer delay other
  recipients of the same broadcast. With the in-memory backplane, the publisher's cancellation token no longer
  cancels writes to other connections (which aborted their sockets) or skips the
  remaining recipients; an already cancelled token still prevents publishing.
  Publishing (`PublishAsync` in 5.0) still completes after local delivery, bounded by
  `BroadcastSendTimeout`.
- AUD-07: `OnCloseAsync` receives a context whose `ConnectionAborted` is the
  `ShutdownTimeout` deadline instead of the already cancelled handler token, so
  asynchronous cleanup runs until the deadline.
- AUD-08: `ConnectionStorage.Add` ignores a closed connection, and shutdown marks
  the connection closing before removing it, so a membership refresh racing with
  disconnect cannot re-register it.
- AUD-09: Document that concurrent actions of one connection share its
  `HttpContext`, `Items`, and ASP.NET `ISession`, which are not thread-safe.
- AUD-11: `IDarkWsContextAccessor.Session` and `HandlerBase.Session` return the
  session the action was authorized with; an `auth:` or `logout` arriving while the
  action runs no longer changes it. The upgrade sets `HttpContext.User` to the
  authenticator's result, so a rejected upgrade no longer keeps the cookie user.
- AUD-12: The data overloads always write `data`, as `null` for a null value and
  even with `DefaultIgnoreCondition = WhenWritingNull`; the Redis envelope keeps an
  explicit null apart from missing data.
- AUD-13: `IWebSocketConnection.Abort()` (default: `WebSocket.Abort()`) is used after
  a broadcast timeout, and a failing abort is logged instead of failing the whole
  broadcast for transportless implementations.
- AUD-14: `DisposeAsync` of the .NET client closes an open connection gracefully
  within `CloseTimeout` before disposing (a connection attempt or an already running
  close is interrupted); `Dispose` still aborts immediately.
- AUD-15: A .NET client system command cancelled or timed out while still queued for
  the socket no longer discards the connection; only a command that may have been
  written does.
- AUD-16: .NET client transport failures name their cause chain by exception type
  and error code, without exception texts that could contain the endpoint token.
- AUD-17: While a failed .NET client connection is torn down, new requests wait for
  the next connection instead of receiving the failing one.
- AUD-18: `AddDarkWsClient` ignores keyed `IDarkWsClient` registrations when it
  checks for duplicates.
- AUD-19: The browser client drops a socket whose `pong` does not arrive within
  `pongTimeout` and reconnects, so half-open connections no longer look open until
  TCP times out.
- AUD-20: A throwing `query()` or `WebSocket` constructor in the browser reconnect
  timer schedules the next attempt instead of ending reconnection.
- AUD-21: Browser `connect()` keeps an open or opening socket instead of replacing
  it and rejecting its requests; use `reconnect()` or `close()` then `connect()`.
- AUD-22: Browser `close(code)` throws `RangeError` for codes other than 1000 and
  3000–4999 before changing any state.
- AUD-23: Browser request ids fall back to `crypto.getRandomValues()`, so pages
  outside a secure context can send requests.
- AUD-24: With `reconnect: false`, browser requests waiting for a connection fail
  when the socket closes instead of at `waitConnectionTimeout`.
- AUD-25: Blank handler or action names and names with surrounding whitespace fail
  registration.
- AUD-26: Each `auth:` command resolves `IDarkWsAuthenticator` from a new scope
  instead of reusing the upgrade request's instance for the whole connection.
- AUD-27: The default authenticator keeps one fallback session id per connection
  across re-authentication, and its documentation states that it does not
  validate `auth:` tokens.
- AUD-28: The browser pong test sends a real text `pong` instead of a JSON string
  that only passed through JSON parsing.
- AUD-31: `check-version.ps1` also compares both versions in
  `packages/darkws/package-lock.json`, and `test-version.ps1` covers a stale lockfile.
- AUD-32: The .NET client specification describes the flat broadcast envelope, and
  the changelog links cover 4.0.0.
- AUD-33: Document that the protocol uses text frames only; the server currently
  also processes binary frames, and the .NET client rejects them.
- AUD-35: The coverage gate instruments only the published assemblies and removes
  its temporary coverage and settings files; the package gate removes its temporary
  directories.
- AUD-37: `WebSocketConnection` and the .NET client signal cancellation after
  leaving their internal locks, so cancellation callbacks and continuations no
  longer run while those locks are held.
- AUD-40: Document that Redis Pub/Sub delivers at most once, so broadcasts published
  while an instance is disconnected from Redis are lost for its clients.
- AUD-41: .NET client requests use `JsonOptions.Encoder`, as server messages do,
  instead of always escaping characters such as `<`, `&`, and non-ASCII text.
- AUD-43: Tests check Redis unsubscription against a subscriber on the same
  channel, count lifecycle hooks atomically, wait for handlers explicitly instead
  of relying on a synchronous start, and read complete raw messages.

## [4.0.0] - 2026-09-15

### Changed

- Breaking wire protocol change: request arguments move from `payload` to `data`,
  broadcasts become `{ "id": "@", "action": "...", "data": ... }`, and authentication
  and logout use text commands `auth:<token>` / `logout` with replies
  `auth:success`, `auth:failed`, and `logout:success`.
- The browser `message` event now exposes the full flat broadcast envelope.
  The .NET client reads the same schema. Application response/error envelopes,
  text heartbeat, and the Redis backplane envelope are unchanged.
- Authentication/logout run sequentially in both clients. A lost acknowledgement
  discards the connection; .NET cancellation of an in-flight system command does
  the same. Token values are excluded from system-command diagnostic metadata.

### Migration

- Upgrade server and clients together. Custom clients must rename request
  `payload` to `data`, read broadcast `action` at the top level, and replace JSON
  authentication/logout requests with the text commands above. Text authentication
  no longer emits a JSON `@auth` response. `AuthenticationFailedError` is retained
  for source compatibility but does not customize the fixed `auth:failed` reply.
  No automatic fallback to the previous JSON schema is provided.
- Roll back server and clients together. There is no persisted-data migration.
  The CLR `InputMessage.Payload` property and SDK method payload parameters keep
  their source names; their wire field is `data`.

### Added

- `DarkWS.Client`: a standalone .NET 8/9/10 client with lazy connection, typed
  requests and subscriptions, acknowledged authentication/logout, cancellation,
  bounded queues, text heartbeat, and reconnect without replaying commands.
- `DarkWS.Client.DependencyInjection`: optional lazy singleton registration of
  `IDarkWsClient`, with container-owned disposal and explicit per-session examples.
- Public API baselines, real-socket integration tests, and isolated installed-package
  consumer checks for both client packages, including a console without ASP.NET.

### Compatibility

- The client uses the server's wire schema; see the 4.0.0 protocol migration
  above. Acknowledged authentication requires matching
  server and client versions.
- Client connection readiness, notification backpressure, and pong deadlines are
  documented in its README. Client Native AOT/mobile/legacy runtime support is not
  certified in this release.

## [3.0.0] - 2026-09-14

### Migration from 2.x

- Upgrade the server before the browser client. `authenticate()` now waits for a
  correlated acknowledgement; handle rejection, which clears the previous session.
- Declare nullable payload parameters where omitted/null input is intentional.
  Invalid payloads return `invalid-request`; malformed registrations and options
  fail startup. Remove unsupported `[Authorize]` metadata and enforce domain
  permissions explicitly in handlers.
- Review connection limits: 16 in-flight requests, a 30-second send timeout,
  30-second keep-alive/PONG deadlines on .NET 9/10, and a 2-minute receive-idle
  timeout on .NET 8. Idle .NET 8 clients must send application traffic.
- Refresh indexed membership after external group changes with
  `ConnectionStorage.Add(connection)`. Use canonical Redis envelope settings;
  coordinate migration if older peers used customized envelope field names.

### Fixed

- DW-036/DW-038/DW-039: Document JIT/trimming limitations, query-token logging,
  short-lived/post-connect authentication, and the trusted Redis channel boundary.
  Clarify that Redis logical database numbers do not isolate Pub/Sub.
- DW-037: Add manually run serialization, fanout, and request-parsing measurements
  with sample medians and allocation counts, without a new benchmark dependency.
- DW-040: Retain three target frameworks; their liveness implementations now differ.
- DW-041: Reject unsupported `[Authorize]`/`IAuthorizeData` on handlers and actions
  at registration, including inherited metadata. Domain policies remain host-owned.
- DW-042: Enforce public API baselines in normal builds and CI; separate the
  shipped 2.1.0 API from reviewed unshipped changes and track nullability.

- DW-018/DW-019: Freeze numeric backplane targets (0/1/2/3), use explicit field
  names, and isolate Redis envelope serialization from application JSON options.
- DW-020/DW-026: Preserve domain codes and inner causes in exceptions; validate
  public boundary arguments and reject empty error codes.
- DW-021: Reserve `@` and `@auth` request ids. Invalid requests using either id
  receive `invalid-request` with an empty response id, avoiding broadcast routing.
- DW-022: Reject duplicate Redis subscriptions and propagate subscription
  lifetime cancellation to active delivery.
- DW-023: Log successful actions and malformed client input at Debug, using
  cached LoggerMessage delegates and level-gated elapsed-time calculation.
- DW-024/DW-025: Verify all option validation and the documented declared-only
  action discovery contract with regression tests.
- DW-028/DW-029/DW-030/DW-031/DW-034/DW-035: Verify installed NuGet consumers,
  archive CI package artifacts, pin SDK 10.0.401, centralize build/package settings,
  remove the redundant Redis framework reference, build npm output on prepack,
  and test Redis dependencies 2.13.17 and 3.2.1 in CI.
- DW-032/DW-033: Treat the presence of an error field as failure, resolve connection
  waits on events, and run ping timers only while connected.

- DW-005: Fail clearly when message context is accessed outside an initialized
  scope; document the explicit lifecycle-hook context.
- DW-006: Honor standard Configure, binding, and PostConfigure registrations.
  Validate final options on resolution and host startup instead of eagerly in
  AddDarkWs. Invalid values now raise OptionsValidationException.
- DW-007: Remove the inaccessible session mutator from IWebSocketConnection so
  consumer assemblies can implement test doubles.
- DW-008/DW-009: Classify malformed, overflowing, and missing required payloads
  as invalid-request before handler invocation; honor nullable parameters.
- DW-010: Serialize a broadcast envelope once and share its immutable bytes.
- DW-011/DW-012/DW-013/DW-017: Bound sends and the whole shutdown, remove closing
  connections from storage, cancel/observe handler tasks, and coordinate resource
  disposal with active I/O. Uncooperative application tasks retain resources until
  actual completion; their late responses are suppressed.
- DW-014: Add acknowledged darkws:authenticate and darkws:logout requests plus
  npm authenticate/logout methods. Failure clears the previous session. Legacy
  auth input receives an @auth response. Deploy the matching server before using
  the new npm methods. Token lifetime enforcement remains host-owned.
- DW-015: Include documented XML API files and portable symbols with embedded
  sources in both NuGet packages; verify package contents in the test gate.
- DW-016: Index session/group recipients and refresh membership on replacement,
  re-authentication, and logout. External group changes require re-adding the
  connection to refresh its snapshot.

- DW-001: Bound concurrent requests per connection to 16 by default, configurable
  with `MaxConcurrentRequestsPerConnection`. Saturated connections apply read
  backpressure; hosts remain responsible for total concurrent connection limits.
- DW-002: Reduce the default keep-alive interval from one hour to 30 seconds.
  Enable a 30-second transport PONG timeout on .NET 9/10. On .NET 8, abort pending
  reads after 2 minutes without incoming application data. Configure these with
  `KeepAliveTimeout` and `ReceiveIdleTimeout`. Silent .NET 8 clients must now send
  traffic within the idle timeout, even when they respond to transport PINGs.
- DW-003: Reject repeated `AddDarkWs()` calls with `InvalidOperationException`
  instead of silently replacing the action registry.
- DW-004: Reject unsupported attributed action signatures during registration
  with the handler type, method, and reason. Such methods were previously ignored.
  Actions are still scanned only on the concrete handler; inheritance rules are
  documented explicitly.

### Deprecated

- DW-027: `Configuration` and `RedisConfiguration` remain as obsolete static
  forwarding wrappers for one major-version cycle. Use
  `DarkWsServiceCollectionExtensions`, `DarkWsEndpointRouteBuilderExtensions`,
  and `DarkWsRedisServiceCollectionExtensions`; ordinary extension calls keep
  working without source changes.

### Changed

These changes affect runtime behavior and startup validation. Existing public
method signatures are preserved except the formerly inaccessible interface
mutator; acknowledged authentication extends the wire protocol.

## [2.1.0]

### Changed

- **Behavior change from 2.0.0:** complete incoming messages are limited to
  **1 MiB (1048576 bytes)** by default, including all fragments and authentication
  frames. Exceeding the limit closes the connection with WebSocket status **1009**.
  Previously accepted larger messages therefore require explicit configuration
  when upgrading, even though public constructor/source compatibility is retained.

Migration for applications that intentionally receive larger messages:

```csharp
builder.Services.AddDarkWs(options => {
    options.MaxMessageSizeBytes = 16 * 1024 * 1024; // Choose a bounded application limit.
});
```

Select the smallest limit that covers legitimate traffic and test large and
fragmented messages before rollout. The three-argument WebSocketConnection
constructor remains available. This entry records the behavior of the already
released 2.1.0; it does not retroactively change its version or defaults.

[Unreleased]: https://github.com/Bobsans/DarkWS/compare/v4.0.0...HEAD
[4.0.0]: https://github.com/Bobsans/DarkWS/compare/v3.0.0...v4.0.0
[3.0.0]: https://github.com/Bobsans/DarkWS/compare/v2.1.0...v3.0.0
[2.1.0]: https://github.com/Bobsans/DarkWS/compare/v2.0.0...v2.1.0
