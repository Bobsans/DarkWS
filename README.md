# DarkWS

[![CI](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml/badge.svg)](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml)
[![Coverage](https://img.shields.io/badge/coverage-90%25%2B-brightgreen)](scripts/test-coverage.ps1)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/v/DarkWS.svg?label=NuGet)](https://www.nuget.org/packages/DarkWS)
[![NuGet Redis](https://img.shields.io/nuget/v/DarkWS.Redis.svg?label=NuGet%20Redis)](https://www.nuget.org/packages/DarkWS.Redis)
[![npm](https://img.shields.io/npm/v/darkws.svg?label=npm)](https://www.npmjs.com/package/darkws)
[![License](https://img.shields.io/github/license/Bobsans/DarkWS)](LICENSE)

DarkWS is a small request/response protocol over WebSockets for ASP.NET Core
and browsers. It provides typed handlers, per-message dependency injection
scopes, authenticated sessions, targeted broadcasts, and an optional Redis
backplane.

## Packages

| Package | Purpose |
| --- | --- |
| [`DarkWS`](DarkWS/) | ASP.NET Core server with an in-memory backplane |
| [`DarkWS.Redis`](DarkWS.Redis/) | Redis backplane for multi-instance deployments |
| [`darkws`](packages/darkws/) | Dependency-free ESM browser client with TypeScript declarations |

The .NET packages target .NET 8, 9, and 10. The browser package targets modern
browsers with native `WebSocket` and `crypto.randomUUID()` support.

Native AOT and trimmed publishing (`PublishAot` / `PublishTrimmed`) are not
supported: handler discovery, compiled expression delegates, and JSON serialization
use reflection/runtime code generation. Use ordinary JIT publishing.
The three target frameworks are intentional: .NET 8 uses application receive-idle
detection, while .NET 9/10 use transport PING/PONG timeouts.

## Features

- Request/response correlation over one WebSocket connection.
- Attribute-based sync and async handlers.
- A new async DI scope for every request.
- Typed application sessions plus optional ASP.NET `ISession` access.
- Authentication on connect and re-authentication without reconnecting.
- Broadcasts to all clients, one connection, one session, or one group.
- In-memory single-instance operation with no extra dependency.
- Optional Redis fan-out for multi-instance deployments.
- Serialized socket writes, bounded sends, graceful shutdown, and reconnects.
- Stable error types and configurable timeouts.

## Installation

Server:

```bash
dotnet add package DarkWS
```

Optional Redis backplane:

```bash
dotnet add package DarkWS.Redis
```

Browser client:

```bash
npm install darkws
```

## ASP.NET Core quick start

Register DarkWS and the assembly containing handlers:

```csharp
using DarkWS;

builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<Program>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets();
app.MapDarkWs("/ws");
```

Authenticated ASP.NET users automatically receive a basic DarkWS session.
Handlers require authentication by default. Mark a handler or action with
`[AllowAnonymous]` when it must be public.

DarkWS action authorization supports only this authenticated/anonymous distinction.
`[Authorize]`, role/policy attributes, and other `IAuthorizeData` on a handler or
action fail registration with `InvalidOperationException`; they are not silently
ignored. Enforce domain permissions inside actions, using `ErrorResponseException`
for controlled denials. HTTP endpoint authorization applies to the upgrade request
and does not implement per-action policies.

```csharp
using DarkWS;
using Microsoft.AspNetCore.Authorization;

[Handler("system"), AllowAnonymous]
public sealed class SystemHandler : HandlerBase {
    [Action("ping")]
    public IResponse Ping() => Ok(new { ServerTime = DateTimeOffset.UtcNow });
}
```

The browser sends `system:ping` and receives the returned object under `data`.

Call `AddDarkWs()` once per service collection; a second call throws
`InvalidOperationException` before changing registrations. Reuse the returned
builder to register additional handler assemblies.

The canonical static entry points are `DarkWsServiceCollectionExtensions` and
`DarkWsEndpointRouteBuilderExtensions` (Redis uses
`DarkWsRedisServiceCollectionExtensions`). The old `Configuration` and
`RedisConfiguration` static calls remain available as obsolete forwarding
wrappers until the next major release. Extension-call syntax is unchanged.

Actions must be public instance methods declared on the scanned handler class,
return exactly `IResponse` or `Task<IResponse>`, and accept zero or one payload
parameter. Generic methods, by-reference/byref-like/pointer payloads, and other
signatures marked with `[Action]` fail registration with the type, method, and
reason. Inherited methods are not scanned; declare or override actions on the
concrete handler and mark them with `[Action]`.

## Connection limits and liveness

```csharp
builder.Services.AddDarkWs(options => {
    options.MaxConcurrentRequestsPerConnection = 16;
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
    options.KeepAliveTimeout = TimeSpan.FromSeconds(30); // .NET 9 and later
    options.ReceiveIdleTimeout = TimeSpan.FromMinutes(2); // .NET 8
});
```

These are the defaults. The request limit must be positive. Once it is reached,
DarkWS waits for a request to finish before dispatching the next one, applying
backpressure to socket reads without an unbounded queue. Responses may arrive
out of order; correlate them by `id`. Slow handlers also delay reading control
messages when the connection is saturated. The host application or reverse proxy
must enforce a total concurrent connection limit and any per-user/IP limits;
DarkWS only bounds requests within each connection.

On .NET 9/10, transport PING/PONG detects unresponsive peers using
`KeepAliveInterval` and `KeepAliveTimeout`. On .NET 8, `ReceiveIdleTimeout` bounds
each pending socket read, resetting after every received fragment; a timeout
aborts the socket and removes the connection during cleanup. Idle .NET 8 clients
must send application traffic (for example, text `ping`) within this timeout.
The browser client sends `ping` every 30 seconds by default. The receive timer
does not run while server backpressure pauses reads. A fragment can also be a
partial read of a frame; transport PONGs do not count as application traffic.
Timeout options must be positive and at most 4294967294 milliseconds.
Choose timeouts that tolerate expected handler latency and client timer throttling.

## Typed sessions

Applications can attach immutable domain data to each connection:

```csharp
using System.Security.Claims;
using DarkWS.Abstractions;

public sealed record AppSession(
    string Id,
    ClaimsPrincipal User,
    Guid AccountId,
    Guid UserId
) : IDarkWsSession {
    public IReadOnlyCollection<string> Groups =>
        [$"account:{AccountId}", $"user:{UserId}"];
}
```

Register an `IDarkWsAuthenticator` that returns `AppSession`:

```csharp
builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<Program>()
    .AddAuthenticator<AppAuthenticator, AppSession>();
```

Typed handlers receive the same session through inheritance and DI:

```csharp
[Handler("message")]
public sealed class MessageHandler(MessageService messages)
    : HandlerBase<AppSession> {
    [Action("send")]
    public async Task<IResponse> SendAsync(MessageInput input) {
        var result = await messages.SendAsync(Session.UserId, input);
        await BroadcastToGroupAsync(
            $"account:{Session.AccountId}",
            "message:created",
            result
        );
        return Ok(result);
    }
}
```

Scoped services can inject `IDarkWsContextAccessor` to access the current
DarkWS session, `HttpContext`, connection, and cancellation token.
This works only in the initialized message scope. Access from ordinary HTTP or
connection scopes throws `InvalidOperationException`; lifecycle middleware must
use the context passed to its hook. A nullable action parameter permits missing
or null payloads. Non-nullable parameters require a payload; malformed values and
numeric overflows return `darkws:error:invalid-request` without invoking the handler.

Configure options through `AddDarkWs`, `services.Configure<DarkWsOptions>`, binding,
or `PostConfigure`. Registrations run in the standard Options order. Validation
runs when options are resolved and on host startup (`OptionsValidationException`).
Options are captured by the connection or singleton service that consumes them;
this does not promise live reconfiguration of existing connections/backplanes.

`SendTimeout` defaults to 30 seconds and covers both waiting for the send lock
and writing to the socket. `BroadcastSendTimeout` can impose a shorter deadline
on broadcasts. `ShutdownTimeout` is one shared deadline for pending handlers,
close hooks, and the close handshake. Shutdown removes the connection from
storage immediately and cancels handler tokens. The socket is aborted when close
cannot finish in time. Handlers must observe `ConnectionAborted`: .NET cannot
forcibly stop arbitrary application code. If a handler ignores cancellation,
its scope and connection resources are retained until it finishes; no response
is sent afterward. Disposal coordinates with concurrent socket operations.

Session/group broadcasts use indexes. Session groups are snapshotted when a
connection is added or re-authenticated. If an application changes group membership
on its own, call `ConnectionStorage.Add(connection)` to refresh the indexes.

For ASP.NET session state, register `AddSession()` and place `UseSession()`
before `MapDarkWs()`. WebSockets are long-lived requests, so call
`HttpContext.Session.CommitAsync()` when a change must be persisted immediately.

## Redis backplane

The core package uses the in-memory backplane by default. Redis requires an
existing `IConnectionMultiplexer` and an explicit channel name:

```csharp
using DarkWS.Redis;
using StackExchange.Redis;

builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddDarkWsRedis("my-app:production");
```

Use a unique channel per application and environment. Handler code does not
change when the backplane changes.

The Redis envelope is independent of application `JsonOptions`: its fixed fields
are `target`, `targetId`, `action`, and `data`, with numeric targets All=0,
Connection=1, Session=2, Group=3. These values must never be reassigned. Application
payloads retain their configured JSON representation inside `data`. Default-format
older peers remain compatible. Coordinate migration or change channels if older
instances emitted a custom envelope naming policy.

A Redis backplane supports one active subscription. Unsubscribe before replacing
it; repeated Subscribe calls throw `InvalidOperationException`. Its listener token
is cancelled when the subscription token is cancelled or Unsubscribe runs.
CI exercises StackExchange.Redis 2.13.17 and 3.2.1 without raising the package minimum.

Treat the Redis channel as a trusted boundary: anyone allowed to publish can send
notifications to clients across all instances. Isolate Redis by network access
and channel ACLs, and scope channels per application/environment. Logical Redis
database numbers do **not** isolate Pub/Sub channels ([Redis documentation](https://redis.io/docs/latest/develop/pubsub/#database--scoping)).
`MaxMessageSizeBytes` limits WebSocket input, not Redis messages.

## Browser client

```ts
import DarkWs from "darkws";

const client = new DarkWs({
  secure: location.protocol === "https:",
  host: location.host,
  path: "/ws",
  query: () => ({ token: getAccessToken() }),
}).connect();

const message = await client.request<Message, SendMessageInput>(
  "message:send",
  { text: "Hello" },
);

const unsubscribe = client.on("message", (event) => {
  console.log("Broadcast", event);
});

await client.authenticate(getAccessToken());

// Resolve only after the server confirms the session was cleared.
await client.logout();

unsubscribe();
client.dispose();
```

`dispose()` is terminal: it stops reconnect and ping timers, closes the socket,
and rejects pending requests.

Connection waits resolve on `open` and have a separate `waitConnectionTimeout`
(30 seconds by default). The response `requestTimeout` starts only after sending
(5 minutes by default), so a call can take up to the sum of the two timeouts.
`requestTimeout: 0` disables response expiry; that request stays pending until a
response, disconnect, or disposal. Construction starts no timer; ping starts on
successful connection and stops when it closes. An explicit close rejects connection
waiters immediately. An error response is recognized by the presence of `error`,
including an empty string from an external server; DarkWS itself rejects empty codes.

## Protocol

| Direction | Shape |
| --- | --- |
| Request | `{ "id": string, "action": string, "payload"?: unknown }` |
| Success | `{ "id": string, "data"?: unknown }` |
| Error | `{ "id": string, "error": string, "data"?: unknown }` |
| Broadcast | `{ "id": "@", "data": { "action": string, "data"?: unknown } }` |

Control messages use plain text: the client sends `ping` or `auth:<token>`, and
the server answers `pong` to a ping.

Request ids must be non-empty and must not be `@` or `@auth`. A request using a
reserved id is rejected without invoking its action. The `invalid-request` response
uses an empty id because echoing a reserved id would turn it into a control event.

The acknowledged authentication API uses ordinary correlated requests:
`darkws:authenticate` with a string token payload, and `darkws:logout` without a
payload. These action names are reserved. Success returns `{ "id": "..." }`;
rejection returns `darkws:error:authentication-failed` and clears the previous
session and principal. Both changes notify `OnAuthenticatedAsync`, whose current
session may now be null. Already running actions are not rolled back by logout.
The legacy `auth:<token>` input remains supported and receives a response with
id `@auth`, safely ignored by older clients. The new npm authentication methods
require a server supporting these actions; older servers return `invalid-action`.

The client does not retain tokens for reconnect. Update the application-owned
`query`/authentication source after logout so reconnect cannot restore old
credentials. Token expiry or revocation does not automatically close an existing
connection: enforce session lifetimes and ongoing authorization in the host app.

Tokens in WebSocket URLs can enter proxy/access logs and telemetry. Prefer a
short-lived connection ticket; when the endpoint permits an anonymous upgrade
and the application authenticator supports it, omit the token from `query` and
await `client.authenticate(token)` before protected requests. Use WSS and redact
credentials in URL and message logging. The application owns token validation,
expiry, and revocation. See [ASP.NET Core token logging guidance](https://learn.microsoft.com/en-us/aspnet/core/signalr/security#access-token-logging).

Both NuGet packages include XML API documentation and portable symbol packages
with embedded source files. `scripts/test-packages.ps1` checks every target's XML
and PDB entries after packing.

## Testing

Run the complete build, test, Redis integration, and coverage gate:

```powershell
pwsh ./scripts/test-coverage.ps1
```

Docker must be running for Redis integration tests. Current gates require at
least 90% line coverage and 80% branch coverage for every package.

The gate also packs both NuGet libraries, verifies documentation/symbols, restores
them into a consumer with an isolated package cache, and runs it on all target
frameworks. A fresh npm copy without `dist` is packed to verify the prepack build.
CI retains the resulting packages as artifacts. Successful actions and malformed
client requests are logged at Debug; unexpected handler failures remain warnings.

Manual performance scenarios live in [benchmarks](benchmarks/README.md); they
are not run on every PR. Both libraries enforce reviewed public API baselines
with PublicApiAnalyzers; see [API maintenance](docs/public-api.md).

## Roadmap

- Add an optional high-level browser API for token refresh, safe read retries,
  and application-level error handling.

## Versioning

The build uses the exact SDK in `global.json`. Common project settings live in
`Directory.Build.props`; NuGet versions are centralized in `Directory.Packages.props`.

All three packages use one SemVer version. Update every manifest and the npm
lockfile with one command:

```powershell
pwsh ./scripts/set-version.ps1 2.1.0
```

CI runs `scripts/test-version.ps1` and rejects inconsistent package versions.
Release tags must use the matching `vX.Y.Z` form, including an optional SemVer
prerelease suffix such as `v2.1.0-rc.1`.

Read [CHANGELOG](CHANGELOG.md) before upgrading. The 2.1.0 entry explains the new
1 MiB input limit and migration for applications sending larger messages. Future
changes that reject previously accepted input must include a behavior-change entry,
migration/configuration guidance, and a SemVer compatibility decision before release.
Substantial breaking defaults require a major release or an explicit compatibility
option. The release workflow requires a changelog heading for the published version.

## Publishing

Publishing uses GitHub Actions OIDC trusted publishing. No long-lived NuGet or
npm publish tokens are stored in GitHub.

One-time registry setup:

1. Create GitHub environment `release` and optionally add required reviewers.
2. Add repository variable `NUGET_USER` with the NuGet.org profile name.
3. On NuGet.org, add a trusted publishing policy for owner `Bobsans`, repository
   `DarkWS`, workflow `release.yml`, and environment `release`.
4. On the existing `darkws` npm package, configure its trusted publisher for
   owner `Bobsans`, repository `DarkWS`, workflow `release.yml`, environment
   `release`, with direct `npm publish` allowed.

For each release:

1. Run `scripts/set-version.ps1` and commit the version change.
2. Create a GitHub Release using the matching `vX.Y.Z` tag.
3. The release workflow validates the tag, runs every test and coverage gate,
   packs all artifacts, then publishes both NuGet packages and the npm package.

Prerelease versions require a GitHub prerelease and use the npm `next` tag.
Stable versions use the npm `latest` tag. Re-running a release is safe: NuGet
uses `--skip-duplicate`, and npm skips an already published version.

## Contributing

Keep changes focused, add behavior tests, and run the complete coverage gate
before opening a pull request.

## License

DarkWS is licensed under the [MIT License](LICENSE).
