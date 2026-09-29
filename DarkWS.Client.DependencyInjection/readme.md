<div align="center">

<img src="https://raw.githubusercontent.com/Bobsans/DarkWS/main/assets/icon.png" alt="DarkWS logo" width="96">

# DarkWS.Client.DependencyInjection

[![NuGet](https://img.shields.io/nuget/v/DarkWS.Client.DependencyInjection.svg?label=NuGet)](https://www.nuget.org/packages/DarkWS.Client.DependencyInjection)
[![CI](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml/badge.svg)](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docs](https://img.shields.io/badge/docs-bobsans.github.io%2FDarkWS-blue)](https://bobsans.github.io/DarkWS/clients/dotnet#dependency-injection)
[![License](https://img.shields.io/github/license/Bobsans/DarkWS)](https://github.com/Bobsans/DarkWS/blob/main/LICENSE)

</div>

Microsoft DI registration for [DarkWS.Client](https://www.nuget.org/packages/DarkWS.Client).
References only DI abstractions, not ASP.NET or the Generic Host.

```bash
dotnet add package DarkWS.Client.DependencyInjection
```

```csharp
using DarkWS.Client;
using Microsoft.Extensions.DependencyInjection;

services.AddDarkWsClient(options => {
    options.Endpoint = new Uri("wss://example.com/ws");
});

public sealed class Calculator(IDarkWsClient client) {
    public Task<int> SumAsync(int left, int right) =>
        client.RequestAsync<int>("math:sum", new { left, right });
}
```

`AddDarkWsClient` registers one lazy singleton `IDarkWsClient`: resolving it does not
connect, and the container owns disposal. For a separate session per scope, register
`DarkWsClient` yourself.

## Documentation

- [Dependency injection](https://bobsans.github.io/DarkWS/clients/dotnet#dependency-injection):
  configuration from services, per-scope sessions
- [.NET client](https://bobsans.github.io/DarkWS/clients/dotnet)
- [Документация на русском](https://bobsans.github.io/DarkWS/ru/clients/dotnet#dependency-injection)
