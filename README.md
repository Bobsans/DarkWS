# DarkWS

[![CI](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml/badge.svg)](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml)
[![Coverage](https://img.shields.io/badge/coverage-90%25%2B-brightgreen)](scripts/test-coverage.ps1)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/v/DarkBoy.DarkWS.svg?label=NuGet)](https://www.nuget.org/packages/DarkBoy.DarkWS)
[![NuGet Redis](https://img.shields.io/nuget/v/DarkBoy.DarkWS.Redis.svg?label=NuGet%20Redis)](https://www.nuget.org/packages/DarkBoy.DarkWS.Redis)
[![npm](https://img.shields.io/npm/v/%40darkboy%2Fdarkws.svg?label=npm)](https://www.npmjs.com/package/@darkboy/darkws)
[![License](https://img.shields.io/github/license/Bobsans/DarkWS)](LICENSE)

DarkWS is a small request/response protocol over WebSockets for ASP.NET Core
and browsers. It provides typed handlers, per-message dependency injection
scopes, authenticated sessions, targeted broadcasts, and an optional Redis
backplane.

## Packages

| Package | Purpose |
| --- | --- |
| [`DarkBoy.DarkWS`](DarkBoy.DarkWS/) | ASP.NET Core server with an in-memory backplane |
| [`DarkBoy.DarkWS.Redis`](DarkBoy.DarkWS.Redis/) | Redis backplane for multi-instance deployments |
| [`@darkboy/darkws`](packages/darkws/) | Dependency-free ESM browser client with TypeScript declarations |

The .NET packages target .NET 8, 9, and 10. The browser package targets modern
browsers with native `WebSocket` and `crypto.randomUUID()` support.

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
dotnet add package DarkBoy.DarkWS
```

Optional Redis backplane:

```bash
dotnet add package DarkBoy.DarkWS.Redis
```

Browser client:

```bash
npm install @darkboy/darkws
```

## ASP.NET Core quick start

Register DarkWS and the assembly containing handlers:

```csharp
using DarkBoy.DarkWS;

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

```csharp
using DarkBoy.DarkWS;
using Microsoft.AspNetCore.Authorization;

[Handler("system"), AllowAnonymous]
public sealed class SystemHandler : HandlerBase {
    [Action("ping")]
    public IResponse Ping() => Ok(new { ServerTime = DateTimeOffset.UtcNow });
}
```

The browser sends `system:ping` and receives the returned object under `data`.

## Typed sessions

Applications can attach immutable domain data to each connection:

```csharp
using System.Security.Claims;
using DarkBoy.DarkWS.Abstractions;

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

For ASP.NET session state, register `AddSession()` and place `UseSession()`
before `MapDarkWs()`. WebSockets are long-lived requests, so call
`HttpContext.Session.CommitAsync()` when a change must be persisted immediately.

## Redis backplane

The core package uses the in-memory backplane by default. Redis requires an
existing `IConnectionMultiplexer` and an explicit channel name:

```csharp
using DarkBoy.DarkWS.Redis;
using StackExchange.Redis;

builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddDarkWsRedis("my-app:production");
```

Use a unique channel per application and environment. Handler code does not
change when the backplane changes.

## Browser client

```ts
import DarkWs from "@darkboy/darkws";

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

unsubscribe();
client.dispose();
```

`dispose()` is terminal: it stops reconnect and ping timers, closes the socket,
and rejects pending requests.

## Protocol

| Direction | Shape |
| --- | --- |
| Request | `{ "id": string, "action": string, "payload"?: unknown }` |
| Success | `{ "id": string, "data"?: unknown }` |
| Error | `{ "id": string, "error": string, "data"?: unknown }` |
| Broadcast | `{ "id": "@", "data": { "action": string, "data"?: unknown } }` |

Control messages use plain text: the client sends `ping` or `auth:<token>`, and
the server answers `pong` to a ping.

## Testing

Run the complete build, test, Redis integration, and coverage gate:

```powershell
pwsh ./scripts/test-coverage.ps1
```

Docker must be running for Redis integration tests. Current gates require at
least 90% line coverage and 80% branch coverage for every package.

## Roadmap

- Add an optional high-level browser API for token refresh, safe read retries,
  and application-level error handling.

## Versioning

All three packages use one SemVer version. Update every manifest and the npm
lockfile with one command:

```powershell
pwsh ./scripts/set-version.ps1 1.1.0
```

CI runs `scripts/test-version.ps1` and rejects inconsistent package versions.
Release tags must use the matching `vX.Y.Z` form, including an optional SemVer
prerelease suffix such as `v1.1.0-rc.1`.

## Publishing

Publishing uses GitHub Actions OIDC trusted publishing. No long-lived NuGet or
npm publish tokens are stored in GitHub.

One-time registry setup:

1. Create GitHub environment `release` and optionally add required reviewers.
2. Add repository variable `NUGET_USER` with the NuGet.org profile name.
3. On NuGet.org, add a trusted publishing policy for owner `Bobsans`, repository
   `DarkWS`, workflow `release.yml`, and environment `release`.
4. Publish `@darkboy/darkws@1.0.0` publicly once, then configure its npm
   trusted publisher for owner `Bobsans`, repository `DarkWS`, workflow
   `release.yml`, environment `release`, with direct `npm publish` allowed.
   The first `v1.0.0` workflow run detects and skips that existing npm version.

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
