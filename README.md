<img src="assets/icon.svg" alt="DarkWS" width="128" align="right">

# DarkWS

[![CI](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml/badge.svg)](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml)
[![Coverage](https://img.shields.io/badge/coverage-94%25%2B-brightgreen)](https://bobsans.github.io/DarkWS/performance)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/v/DarkWS.svg?label=NuGet)](https://www.nuget.org/packages/DarkWS)
[![NuGet Redis](https://img.shields.io/nuget/v/DarkWS.Redis.svg?label=NuGet%20Redis)](https://www.nuget.org/packages/DarkWS.Redis)
[![npm](https://img.shields.io/npm/v/darkws.svg?label=npm)](https://www.npmjs.com/package/darkws)
[![Docs](https://img.shields.io/badge/docs-bobsans.github.io%2FDarkWS-blue)](https://bobsans.github.io/DarkWS/)
[![License](https://img.shields.io/github/license/Bobsans/DarkWS)](LICENSE)

**Typed request/response and real-time broadcasts over WebSockets for ASP.NET Core
and the browser.**

Write a handler, call it from the browser, await the result. Push updates to one
user, a group, or everyone, on one server or a cluster. No hubs, no negotiation
endpoint, no code generation: plain JSON over one socket.

**[Documentation](https://bobsans.github.io/DarkWS/)** ·
**[Документация на русском](https://bobsans.github.io/DarkWS/ru/)** ·
[Getting started](https://bobsans.github.io/DarkWS/getting-started) ·
[Build a chat](https://bobsans.github.io/DarkWS/guides/chat)

## In 30 seconds

Server:

```csharp
builder.Services.AddDarkWs().AddHandlersFromAssemblyContaining<Program>();
app.UseWebSockets(new WebSocketOptions { AllowedOrigins = { "https://app.example.com" } });
app.MapDarkWs("/ws");

// Actions require a signed-in user; the ASP.NET Core identity (cookie, JWT) is used by default.
[Handler("todo")]
public sealed class TodoHandler(TodoStore store) : HandlerBase {
    [Action("add")]
    public async Task<IResponse> AddAsync(NewTodo input) {
        var todo = await store.AddAsync(input.Title, ConnectionAborted);
        await PublishAsync(BroadcastTarget.All, "todo:added", todo);
        return Ok(todo);
    }
}
```

Browser:

```ts
import DarkWs from "darkws";

const client = new DarkWs({ secure: true, path: "/ws" }).connect();

const todo = await client.request<Todo>("todo:add", { title: "Ship it" });
client.onAction<Todo>("todo:added", todo => render(todo));
```

.NET:

```csharp
await using var client = new DarkWsClient(new Uri("wss://example.com/ws"));
var todo = await client.RequestAsync<Todo>("todo:add", new { title = "Ship it" });
using var subscription = client.On<Todo>("todo:added", Render);
```

## Why DarkWS

- **Request/response, not just messages.** Every call gets a typed result or a
  typed error code. Responses are correlated by id, so many requests share one
  socket and may complete in any order.
- **Feels like ASP.NET Core.** Handlers get constructor injection and a fresh DI
  scope per request, the ASP.NET identity works out of the box, and options use the
  standard Options pipeline with startup validation.
- **Targeted broadcasts.** Send to everyone, one connection, every tab of a user, or
  a union of groups with the caller excluded, serialized once for all recipients.
- **Scale out in one line.** `.AddRedis("my-app:prod")` fans broadcasts out across
  instances. Handler code does not change.
- **Safe by default.** Actions require authentication unless marked
  `[AllowAnonymous]`. Message size, concurrency, queue length, and send time are
  bounded per connection; origins can be restricted; handler exceptions never leak
  to clients.
- **Resilient clients.** Both clients reconnect with backoff, detect half-open
  sockets with heartbeats, restore the session before sending queued requests, and
  offer opt-in retries for idempotent calls.
- **Testable.** `DarkWS.Testing` runs actions through the real pipeline, including
  authorization, filters, and broadcasts, without a server or a socket.
- **A protocol you can read.** Four JSON shapes and three text commands. Write a
  client for another platform in an afternoon.
- **Small.** The browser client has zero dependencies and is about 6 KB gzipped.

## Fast

Broadcasting to 1 000 local recipients takes about **0.14 ms and 12 KB** of
allocation, because the payload is serialized once and written to all sockets in
parallel (.NET 10, Ryzen 7 3700X, in-memory transport).

| Recipients | Serialize per recipient | DarkWS fanout |
| ---: | ---: | ---: |
| 100 | 0.27 ms | 0.02 ms |
| 1 000 | 2.90 ms | 0.14 ms |
| 10 000 | 6.04 ms | 2.20 ms |

Full numbers and methodology: [Performance](https://bobsans.github.io/DarkWS/performance).

## Tested

| | |
| --- | --- |
| .NET tests | 278, run on .NET 8, 9, and 10 |
| Browser client tests | 127 |
| Line coverage | 94.9–100% per package, gated at 90% |
| Branch coverage | 93.1–100% per package, gated at 80% |
| Redis | Integration tests against real Redis, StackExchange.Redis 2.13 and 3.2 |
| Packages | Every NuGet and npm package is packed, installed into a clean project, and run in CI |
| Public API | Tracked with PublicApiAnalyzers; breaking changes only in major versions |

## Packages

| Package | | Purpose |
| --- | --- | --- |
| `DarkWS` | [![NuGet](https://img.shields.io/nuget/v/DarkWS.svg)](https://www.nuget.org/packages/DarkWS) | ASP.NET Core server with an in-memory backplane |
| `DarkWS.Redis` | [![NuGet](https://img.shields.io/nuget/v/DarkWS.Redis.svg)](https://www.nuget.org/packages/DarkWS.Redis) | Redis backplane for multi-instance deployments |
| `DarkWS.Client` | [![NuGet](https://img.shields.io/nuget/v/DarkWS.Client.svg)](https://www.nuget.org/packages/DarkWS.Client) | Async .NET client |
| `DarkWS.Client.DependencyInjection` | [![NuGet](https://img.shields.io/nuget/v/DarkWS.Client.DependencyInjection.svg)](https://www.nuget.org/packages/DarkWS.Client.DependencyInjection) | Microsoft DI registration for the .NET client |
| `DarkWS.Testing` | [![NuGet](https://img.shields.io/nuget/v/DarkWS.Testing.svg)](https://www.nuget.org/packages/DarkWS.Testing) | Test handlers without sockets |
| `darkws` | [![npm](https://img.shields.io/npm/v/darkws.svg)](https://www.npmjs.com/package/darkws) | Dependency-free browser client with TypeScript types |

```bash
dotnet add package DarkWS
npm install darkws
```

The .NET packages target .NET 8, 9, and 10. Native AOT and trimming are not
supported.

## Learn more

- [Getting started](https://bobsans.github.io/DarkWS/getting-started)
- [Handlers and actions](https://bobsans.github.io/DarkWS/server/handlers) ·
  [Authentication and sessions](https://bobsans.github.io/DarkWS/server/authentication) ·
  [Broadcasts](https://bobsans.github.io/DarkWS/server/broadcasts) ·
  [Redis](https://bobsans.github.io/DarkWS/server/redis)
- [Browser client](https://bobsans.github.io/DarkWS/clients/browser) ·
  [.NET client](https://bobsans.github.io/DarkWS/clients/dotnet)
- [Protocol](https://bobsans.github.io/DarkWS/protocol) ·
  [Security](https://bobsans.github.io/DarkWS/security) ·
  [Upgrading to 5.0](https://bobsans.github.io/DarkWS/upgrading)
- [Changelog](CHANGELOG.md)

## Contributing

Issues and pull requests are welcome. See [docs/development.md](docs/development.md)
for building, testing, and releasing, and [SECURITY.md](SECURITY.md) for reporting
vulnerabilities privately.

## License

[MIT](LICENSE)
