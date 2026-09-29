<div align="center">

<img src="https://raw.githubusercontent.com/Bobsans/DarkWS/main/assets/icon.png" alt="DarkWS logo" width="96">

# DarkWS.Client

[![NuGet](https://img.shields.io/nuget/v/DarkWS.Client.svg?label=NuGet)](https://www.nuget.org/packages/DarkWS.Client)
[![CI](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml/badge.svg)](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docs](https://img.shields.io/badge/docs-bobsans.github.io%2FDarkWS-blue)](https://bobsans.github.io/DarkWS/clients/dotnet)
[![License](https://img.shields.io/github/license/Bobsans/DarkWS)](https://github.com/Bobsans/DarkWS/blob/main/LICENSE)

</div>

Async .NET client for [DarkWS](https://www.nuget.org/packages/DarkWS) servers, with
typed requests, broadcast subscriptions, reconnects, and automatic session
restoration. No runtime package dependencies and no ASP.NET requirement.

```bash
dotnet add package DarkWS.Client
```

```csharp
using DarkWS.Client;

await using var client = new DarkWsClient(new DarkWsClientOptions {
    Endpoint = new Uri("wss://example.com/ws"),
    AuthenticationTokenProvider = ct => tokenStore.GetAccessTokenAsync(ct),
});

var result = await client.RequestAsync<SumResult>("math:sum", new { left = 10, right = 20 });

using var subscription = client.On<int>("math:summed", sum => Console.WriteLine(sum));

public sealed record SumResult(int Value);
```

The first request connects. Server errors throw `DarkWsResponseException` with the
error `Code`. One client owns one server session.

## Documentation

- [.NET client](https://bobsans.github.io/DarkWS/clients/dotnet): requests,
  notifications, authentication, lifecycle, options
- [Protocol and client lifecycle contract](https://bobsans.github.io/DarkWS/protocol#client-lifecycle-contract)
- [Документация на русском](https://bobsans.github.io/DarkWS/ru/clients/dotnet)

For Microsoft DI registration, install `DarkWS.Client.DependencyInjection`.
