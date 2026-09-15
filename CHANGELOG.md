# Changelog

All notable changes are recorded here using versioned entries and the categories
Added, Changed, Deprecated, Removed, Fixed, and Security. Behavior changes must
include migration guidance before a release is published.

## [Unreleased]

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

[Unreleased]: https://github.com/Bobsans/DarkWS/compare/v3.0.0...HEAD
[3.0.0]: https://github.com/Bobsans/DarkWS/compare/v2.1.0...v3.0.0
[2.1.0]: https://github.com/Bobsans/DarkWS/compare/v2.0.0...v2.1.0
