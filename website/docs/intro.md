---
slug: /
sidebar_position: 1
title: Introduction
---

# DarkWS

These pages describe the **5.0.0 source API**. For the released **4.0.0** packages,
select **4.x** in the version selector; use the examples for your package version.

DarkWS is a small request/response protocol over WebSockets for ASP.NET Core
and browsers. You write handler classes with attributed actions; clients call
them by name and await typed results over one long-lived connection. The server
can push broadcasts to every client, one connection, one session, or a set of
groups, on one instance or many through Redis.

```csharp
[Handler("math")]
public sealed class MathHandler : HandlerBase {
    [Action("sum")]
    public IResponse Sum(SumInput input) => Ok(input.Left + input.Right);
}
```

```ts
const total = await client.request<number>("math:sum", { left: 2, right: 3 }); // 5
```

## Features

- Request/response correlation over one WebSocket connection, with responses that
  may arrive out of order.
- Attribute-based sync and async handlers with a fresh DI scope for every request.
- Typed application sessions, optional ASP.NET `ISession` access, authentication on
  connect and re-authentication without reconnecting.
- Broadcasts to all clients, one connection, one session, or a group union with
  exclusions.
- In-memory operation with no extra dependency, and an optional Redis backplane for
  multi-instance deployments.
- Action filters, request filters, scope initializers, and connection lifecycle hooks.
- Bounded per-connection concurrency, message size limits, liveness detection,
  serialized socket writes, and graceful shutdown.
- A dependency-free browser client and an async .NET client, both with reconnects,
  timeouts, and automatic session restoration.
- A test host that runs handlers through the real server pipeline without sockets.

## Packages

| Package | Purpose |
| --- | --- |
| `DarkWS` | ASP.NET Core server with an in-memory backplane |
| `DarkWS.Redis` | Redis backplane for multi-instance deployments |
| `DarkWS.Client` | Async .NET client with typed requests and broadcasts |
| `DarkWS.Client.DependencyInjection` | Optional Microsoft DI registration of `IDarkWsClient` |
| `DarkWS.Testing` | Handler unit tests and registered action tests without sockets |
| `darkws` (npm) | Dependency-free ESM browser client with TypeScript declarations |

All packages share one version number. Upgrade servers and clients together.

## Requirements

- The .NET packages target .NET 8, 9, and 10. The three targets are intentional:
  .NET 8 detects dead connections with an application receive timeout, while
  .NET 9 and later use transport PING/PONG timeouts.
- The browser package targets modern browsers with native `WebSocket`. It also
  runs on any runtime with a global `WebSocket`, such as Node.js 22 and later.
- Native AOT and trimmed publishing are not supported: handler discovery, compiled
  delegates, and JSON serialization use reflection and runtime code generation.

## Where to go next

- [Getting started](getting-started.md) builds a working server and client.
- [Handlers and actions](server/handlers.md) explains how requests reach your code.
- [Browser client](clients/browser.md) and [.NET client](clients/dotnet.md) cover
  the clients.
- [Protocol](protocol.md) documents the wire format for writing your own client.
