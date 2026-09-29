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
dotnet add package DarkWS
```

Browser client:

```bash
npm install darkws
```

.NET client:

```bash
dotnet add package DarkWS.Client
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

app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets(new WebSocketOptions { AllowedOrigins = { "https://app.example.com" } });
app.MapDarkWs("/ws");

app.Run();
```

`MapDarkWs` requires `UseWebSockets`. `AllowedOrigins` rejects upgrades from other
sites with 403; keep it whenever you use cookie authentication (see
[Security](security.md#cross-site-websocket-hijacking)).

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
it. See [Browser client](clients/browser.md) for authentication, retries, and
subscriptions.

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
    await PublishAsync(BroadcastTarget.All, "math:summed", result);
    return Ok(result);
}
```

```ts
client.onAction<{ value: number }>("math:summed", result => {
  console.log("Someone computed", result.value);
});
```

## Test it

`DarkWS.Testing` runs the action through the real pipeline without a server:

```csharp
await using var host = new DarkWsTestHost(builder =>
    builder.AddHandlersFromAssemblyContaining<MathHandler>());
var connection = host.CreateConnection();

await host.InvokeAsync(connection, "math:sum",
    JsonSerializer.SerializeToElement(new { left = 2, right = 3 }), requestId: "1");

// The response is the last message; the math:summed broadcast to All arrives first.
using var response = JsonDocument.Parse(connection.SentMessages.Last());
Assert.That(response.RootElement.GetProperty("data").GetProperty("value").GetInt32(), Is.EqualTo(5));
Assert.That(host.Broadcasts.Single().Action, Is.EqualTo("math:summed"));
```

## Next steps

- [Authentication and sessions](server/authentication.md) to protect actions.
- [Broadcasts](server/broadcasts.md) for targeted notifications.
- [Build a chat](guides/chat.md) for a complete example.
