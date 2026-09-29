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
dotnet add package DarkWS
```

Браузерный клиент:

```bash
npm install darkws
```

.NET-клиент:

```bash
dotnet add package DarkWS.Client
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

app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets(new WebSocketOptions { AllowedOrigins = { "https://app.example.com" } });
app.MapDarkWs("/ws");

app.Run();
```

`MapDarkWs` требует `UseWebSockets`. `AllowedOrigins` отклоняет upgrade-запросы с других
сайтов с кодом 403; оставляйте его всегда, когда используете аутентификацию через cookie (см.
[Безопасность](security.md#cross-site-websocket-hijacking)).

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
Аутентификация, повторные попытки и подписки описаны в разделе [Браузерный клиент](clients/browser.md).

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
    await PublishAsync(BroadcastTarget.All, "math:summed", result);
    return Ok(result);
}
```

```ts
client.onAction<{ value: number }>("math:summed", result => {
  console.log("Someone computed", result.value);
});
```

## Тестирование {#test-it}

`DarkWS.Testing` прогоняет действие через настоящий конвейер без сервера:

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

## Дальнейшие шаги {#next-steps}

- [Аутентификация и сессии](server/authentication.md) — защита действий.
- [Рассылки](server/broadcasts.md) — адресные уведомления.
- [Создание чата](guides/chat.md) — полный пример.
