<div align="center">

<img src="https://raw.githubusercontent.com/Bobsans/DarkWS/main/assets/icon.png" alt="DarkWS logo" width="96">

# DarkWS.Redis

[![NuGet](https://img.shields.io/nuget/v/DarkWS.Redis.svg?label=NuGet)](https://www.nuget.org/packages/DarkWS.Redis)
[![CI](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml/badge.svg)](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docs](https://img.shields.io/badge/docs-bobsans.github.io%2FDarkWS-blue)](https://bobsans.github.io/DarkWS/server/redis)
[![License](https://img.shields.io/github/license/Bobsans/DarkWS)](https://github.com/Bobsans/DarkWS/blob/main/LICENSE)

</div>

Redis backplane for [DarkWS](https://www.nuget.org/packages/DarkWS): broadcasts
published on one server instance reach clients connected to every instance.

```bash
dotnet add package DarkWS.Redis
```

```csharp
using DarkWS.Redis;
using StackExchange.Redis;

builder.Services.AddSingleton<IConnectionMultiplexer>(
    await ConnectionMultiplexer.ConnectAsync(builder.Configuration["Redis"]!));

builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<Program>()
    .AddRedis("my-app:production");
```

Use a unique channel per application and environment. Handler code does not change.
Redis Pub/Sub delivers at most once, and anyone who can publish to the channel can
reach your clients: read the delivery and trust notes before production.

## Documentation

- [Redis backplane](https://bobsans.github.io/DarkWS/server/redis): delivery
  guarantees, ordering, trust boundary, wire format
- [Broadcasts](https://bobsans.github.io/DarkWS/server/broadcasts)
- [Документация на русском](https://bobsans.github.io/DarkWS/ru/server/redis)

Requires StackExchange.Redis 2.13.17 or later; tested with 2.13.17 and 3.2.1.
