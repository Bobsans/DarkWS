<div align="center">

<img src="https://raw.githubusercontent.com/Bobsans/DarkWS/main/assets/icon.png" alt="DarkWS logo" width="96">

# DarkWS.Testing

[![NuGet](https://img.shields.io/nuget/v/DarkWS.Testing.svg?label=NuGet)](https://www.nuget.org/packages/DarkWS.Testing)
[![CI](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml/badge.svg)](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docs](https://img.shields.io/badge/docs-bobsans.github.io%2FDarkWS-blue)](https://bobsans.github.io/DarkWS/server/testing)
[![License](https://img.shields.io/github/license/Bobsans/DarkWS)](https://github.com/Bobsans/DarkWS/blob/main/LICENSE)

</div>

Test [DarkWS](https://www.nuget.org/packages/DarkWS) handlers through the real server
pipeline (authorization, binding, filters, broadcasts) without an HTTP server, a
WebSocket, or Redis. Works with any test framework.

```bash
dotnet add package DarkWS.Testing
```

```csharp
using System.Text.Json;
using DarkWS.Testing;

await using var host = new DarkWsTestHost(builder =>
    builder.AddHandlersFromAssemblyContaining<MathHandler>());
var connection = host.CreateConnection(); // pass an IDarkWsSession for authenticated calls

await host.InvokeAsync(connection, "math:sum",
    JsonSerializer.SerializeToElement(new { left = 2, right = 3 }), requestId: "1");

using var response = JsonDocument.Parse(connection.SentMessages.Last());
Assert.That(response.RootElement.GetProperty("data").GetInt32(), Is.EqualTo(5));
Assert.That(host.Broadcasts, Is.Not.Empty);
```

## Documentation

- [Testing handlers](https://bobsans.github.io/DarkWS/server/testing): sessions,
  broadcasts, direct handler tests, limits of the test host
- [Документация на русском](https://bobsans.github.io/DarkWS/ru/server/testing)
