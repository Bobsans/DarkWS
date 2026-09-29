<div align="center">

<img src="https://raw.githubusercontent.com/Bobsans/DarkWS/main/assets/icon.png" alt="DarkWS logo" width="96">

# DarkWS

[![NuGet](https://img.shields.io/nuget/v/DarkWS.svg?label=NuGet)](https://www.nuget.org/packages/DarkWS)
[![CI](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml/badge.svg)](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docs](https://img.shields.io/badge/docs-bobsans.github.io%2FDarkWS-blue)](https://bobsans.github.io/DarkWS/)
[![License](https://img.shields.io/github/license/Bobsans/DarkWS)](https://github.com/Bobsans/DarkWS/blob/main/LICENSE)

</div>

Typed request/response and real-time broadcasts over WebSockets for ASP.NET Core.

```bash
dotnet add package DarkWS
```

```csharp
using DarkWS;
using Microsoft.AspNetCore.Authorization;

builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<Program>();

app.UseWebSockets(new WebSocketOptions { AllowedOrigins = { "https://app.example.com" } });
app.MapDarkWs("/ws");

[Handler("math"), AllowAnonymous]
public sealed class MathHandler : HandlerBase {
    [Action("sum")]
    public async Task<IResponse> SumAsync(SumInput input) {
        var result = input.Left + input.Right;
        await PublishAsync(BroadcastTarget.All, "math:summed", result);
        return Ok(result);
    }
}

public sealed record SumInput(int Left, int Right);
```

Clients call `math:sum` with `{ "left": 2, "right": 3 }` and receive `5`. Actions
require an authenticated session unless marked `[AllowAnonymous]`.

## Documentation

- [Getting started](https://bobsans.github.io/DarkWS/getting-started)
- [Handlers and actions](https://bobsans.github.io/DarkWS/server/handlers)
- [Authentication and sessions](https://bobsans.github.io/DarkWS/server/authentication)
- [Broadcasts](https://bobsans.github.io/DarkWS/server/broadcasts)
- [Configuration and limits](https://bobsans.github.io/DarkWS/server/configuration)
- [Upgrading to 5.0](https://bobsans.github.io/DarkWS/upgrading)
- [Документация на русском](https://bobsans.github.io/DarkWS/ru/)

Related packages: `DarkWS.Redis` (multi-instance backplane), `DarkWS.Testing`
(handler tests), `DarkWS.Client` (.NET client), and `darkws` on npm (browser client).
