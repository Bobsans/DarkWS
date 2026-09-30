---
sidebar_position: 2
title: Начало работы
---

# Начало работы

На этой странице мы соберём сервер ASP.NET Core с одним публичным действием и вызовем его
из браузера и из .NET.

## Установка {#install}

Сервер:

```bash
dotnet add package DarkWS --version 4.0.0
```

Браузерный клиент:

```bash
npm install darkws@4
```

.NET-клиент:

```bash
dotnet add package DarkWS.Client --version 4.0.0
```

## Сервер {#server}

Зарегистрируйте DarkWS, добавьте сборку с вашими обработчиками и сопоставьте конечную точку:

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

`MapDarkWs` требует `UseWebSockets`. `AllowedOrigins` отклоняет upgrade-запросы с других
сайтов с кодом 403; оставляйте его всегда, когда используете аутентификацию через cookie (см.
[Безопасность](security.md)).

Добавьте обработчик. По умолчанию обработчики требуют аутентифицированной сессии, поэтому этот
помечен `[AllowAnonymous]`:

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

Действие вызывается как `math:sum`: имя обработчика, двоеточие и имя действия.
По умолчанию JSON следует веб-соглашениям, поэтому payload — `{ "left": 2, "right": 3 }`,
а результат — `{ "value": 5 }`.

## Браузерный клиент {#browser-client}

```ts
import DarkWs from "darkws";

const client = new DarkWs({
  secure: location.protocol === "https:",
  path: "/ws",
}).connect();

const result = await client.request<{ value: number }>("math:sum", { left: 2, right: 3 });
console.log(result.value); // 5
```

По умолчанию `host` равен `location.host`. Запросы, сделанные до открытия сокета, ждут его.
Аутентификация, переподключение и рассылки описаны в разделе [Браузерный клиент](clients/browser.md).

## .NET-клиент {#net-client}

```csharp
using DarkWS.Client;

await using var client = new DarkWsClient(new Uri("wss://example.com/ws"));
var result = await client.RequestAsync<SumResult>("math:sum", new { left = 2, right = 3 });
Console.WriteLine(result.Value); // 5
```

Первый запрос подключается автоматически. См. [.NET-клиент](clients/dotnet.md).

## Отправка рассылки {#push-a-broadcast}

Обработчики и любой сервис с внедрённым `IBroadcaster` могут публиковать уведомления:

```csharp
[Action("sum")]
public async Task<IResponse> SumAsync(SumInput input) {
    var result = new SumResult(input.Left + input.Right);
    await BroadcastAsync("math:summed", result);
    return Ok(result);
}
```

Событие `message` браузерного клиента получает каждый конверт рассылки
`{ id: "@", action, data }`; фильтруйте его по `action`:

```ts
client.on("message", (message) => {
  const broadcast = message as { action: string; data?: { value: number } };
  if (broadcast.action === "math:summed") {
    console.log("Someone computed", broadcast.data?.value);
  }
});
```

## Тестирование {#test-it}

В DarkWS 4.x нет отдельного пакета для тестов. Запустите приложение на свободном
loopback-порту и вызовите его через `DarkWS.Client`:

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

## Дальнейшие шаги {#next-steps}

- [Аутентификация и сессии](server/authentication.md) — защита действий.
- [Рассылки](server/broadcasts.md) — адресные уведомления.
- [Создание чата](guides/chat.md) — полный пример.
