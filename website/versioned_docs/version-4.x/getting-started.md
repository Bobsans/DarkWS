---
sidebar_position: 2
title: Getting started
---

# Getting started

This page builds an ASP.NET Core server with one public action and calls it from a
browser and from .NET.

## Install

Server:

```bash
dotnet add package DarkWS --version 4.0.0
```

Browser client:

```bash
npm install darkws@4
```

.NET client:

```bash
dotnet add package DarkWS.Client --version 4.0.0
```

## Server

Register DarkWS, add the assembly that contains your handlers, and map an endpoint:

```csharp
using DarkWS;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<Program>();

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions { AllowedOrigins = { "https://app.example.com" } });
app.MapDarkWs("/ws");

app.Run();
```

`MapDarkWs` requires `UseWebSockets`. `AllowedOrigins` rejects upgrades from other
sites with 403; keep it whenever you use cookie authentication (see
[Security](security.md)).

Add a handler. Handlers require an authenticated session by default, so this one is
marked `[AllowAnonymous]`:

```csharp
using DarkWS;
using Microsoft.AspNetCore.Authorization;

[Handler("math"), AllowAnonymous]
public sealed class MathHandler : HandlerBase {
    [Action("sum")]
    public IResponse Sum(SumInput input) => Ok(new SumResult(input.Left + input.Right));
}

public sealed record SumInput(int Left, int Right);
public sealed record SumResult(int Value);
```

The action is called as `math:sum`: the handler name, a colon, and the action name.
JSON uses web conventions by default, so the payload is `{ "left": 2, "right": 3 }`
and the result is `{ "value": 5 }`.

## Browser client

```ts
import DarkWs from "darkws";

const client = new DarkWs({
  secure: location.protocol === "https:",
  path: "/ws",
}).connect();

const result = await client.request<{ value: number }>("math:sum", { left: 2, right: 3 });
console.log(result.value); // 5
```

`host` defaults to `location.host`. Requests made before the socket opens wait for
it. See [Browser client](clients/browser.md) for authentication, reconnects, and
broadcasts.

## .NET client

```csharp
using DarkWS.Client;

await using var client = new DarkWsClient(new Uri("wss://example.com/ws"));
var result = await client.RequestAsync<SumResult>("math:sum", new { left = 2, right = 3 });
Console.WriteLine(result.Value); // 5
```

The first request connects automatically. See [.NET client](clients/dotnet.md).

## Push a broadcast

Handlers and any service with an injected `IBroadcaster` can publish notifications:

```csharp
[Action("sum")]
public async Task<IResponse> SumAsync(SumInput input) {
    var result = new SumResult(input.Left + input.Right);
    await BroadcastAsync("math:summed", result);
    return Ok(result);
}
```

The browser `message` event receives every broadcast envelope
`{ id: "@", action, data }`; filter it by `action`:

```ts
client.on("message", (message) => {
  const broadcast = message as { action: string; data?: { value: number } };
  if (broadcast.action === "math:summed") {
    console.log("Someone computed", broadcast.data?.value);
  }
});
```

## Test it

DarkWS 4.x has no dedicated test package. Start the application on a free loopback
port and call it with `DarkWS.Client`:

```csharp
using System.Net;
using DarkWS;
using DarkWS.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder();
builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
builder.Services.AddDarkWs().AddHandlersFromAssemblyContaining<MathHandler>();

await using var app = builder.Build();
app.UseWebSockets();
app.MapDarkWs("/ws");
await app.StartAsync();

var address = app.Services.GetRequiredService<IServer>()
    .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
await using var client = new DarkWsClient(new Uri(address.Replace("http://", "ws://") + "/ws"));

var result = await client.RequestAsync<SumResult>("math:sum", new { left = 2, right = 3 });
Assert.That(result.Value, Is.EqualTo(5));
```

## Next steps

- [Authentication and sessions](server/authentication.md) to protect actions.
- [Broadcasts](server/broadcasts.md) for targeted notifications.
- [Build a chat](guides/chat.md) for a complete example.
