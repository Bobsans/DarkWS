---
title: Создаём чат
---

# Создаём чат

В этом руководстве создаётся небольшая чат-комната: пользователи выбирают ник, видят недавнюю историю,
отправляют сообщения и видят, кто входит и выходит. Используются типизированные сессии, группы,
рассылки, middleware соединения и браузерный клиент.

## Сессия и аутентификатор {#session-and-authenticator}

Каждое соединение вступает в группу `chat`. Для демо токен — это просто
ник, который передаётся в query-параметре `token` запроса upgrade; настоящее
приложение проверяет здесь токен. Если аутентификатор возвращает `null`, соединение
всё равно принимается, но без сессии.

```csharp
using System.Security.Claims;
using DarkWS.Abstractions;

public sealed record ChatSession(string Id, ClaimsPrincipal User, string Name) : IDarkWsSession {
    public IReadOnlyCollection<string> Groups => ["chat"];
}

public sealed class NicknameAuthenticator : IDarkWsAuthenticator {
    public ValueTask<IDarkWsSession?> AuthenticateAsync(
        HttpContext context, string? token, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 32) {
            return ValueTask.FromResult<IDarkWsSession?>(null);
        }
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, token)], "nickname"));
        return ValueTask.FromResult<IDarkWsSession?>(new ChatSession(Guid.NewGuid().ToString("N"), user, token));
    }
}
```

Идентичности нужен тип аутентификации (здесь `"nickname"`): действиям без
`[AllowAnonymous]` нужна сессия с аутентифицированным пользователем.

## История {#history}

Singleton хранит последние 50 сообщений:

```csharp
public sealed record ChatMessage(string Author, string Text, DateTimeOffset SentAt);

public sealed class ChatHistory {
    private readonly object _lock = new();
    private readonly Queue<ChatMessage> _messages = new();

    public void Add(ChatMessage message) {
        lock (_lock) {
            _messages.Enqueue(message);
            if (_messages.Count > 50) _messages.Dequeue();
        }
    }

    public ChatMessage[] Latest() {
        lock (_lock) return [.. _messages];
    }
}
```

При нескольких экземплярах сервера храните историю в общем хранилище, например в базе данных.

## Обработчик {#handler}

```csharp
using DarkWS;

[Handler("chat")]
public sealed class ChatHandler(ChatHistory history) : HandlerBase<ChatSession> {
    [Action("history")]
    public IResponse History() => Ok(history.Latest());

    [Action("send")]
    public async Task<IResponse> SendAsync(SendInput input) {
        if (string.IsNullOrWhiteSpace(input.Text) || input.Text.Length > 500) {
            return Error("chat:invalid-text");
        }
        var message = new ChatMessage(Session.Name, input.Text.Trim(), DateTimeOffset.UtcNow);
        history.Add(message);
        await BroadcastToGroupAsync("chat", "chat:message", message);
        return Ok();
    }
}

public sealed record SendInput(string Text);
```

Рассылка группе доходит до каждого участника, включая отправителя, поэтому все
клиенты отображают сообщения одинаково. DarkWS 4.x не умеет исключать получателя из
рассылки группе.

## Присутствие {#presence}

Middleware соединения сообщает о входе и выходе:

```csharp
using DarkWS;
using DarkWS.Abstractions;

public sealed class PresenceMiddleware(IBroadcaster broadcaster) : DarkWsMiddleware {
    public override Task OnOpenAsync(IDarkWsContextAccessor context) =>
        Announce(context, "chat:joined");

    public override Task OnCloseAsync(IDarkWsContextAccessor context) =>
        Announce(context, "chat:left");

    private Task Announce(IDarkWsContextAccessor context, string action) =>
        context.Session is ChatSession session
            ? broadcaster.BroadcastToGroupAsync("chat", action, new { session.Name })
            : Task.CompletedTask;
}
```

`AddHandlersFromAssemblyContaining<T>()` регистрирует каждый неабстрактный наследник
`DarkWsMiddleware` в сканируемой сборке, поэтому отдельная регистрация middleware не
нужна.

`OnOpenAsync` выполняется после регистрации соединения, поэтому новый участник тоже
получает свой `chat:joined`. `OnCloseAsync` выполняется после удаления соединения,
поэтому уходящий участник не получает `chat:left`. К моменту запуска `OnCloseAsync`
токен `ConnectionAborted` из контекста уже отменён, поэтому пример не передаёт его в
рассылку.

## Program {#program}

```csharp
using DarkWS;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ChatHistory>();
builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<ChatHandler>()
    .AddAuthenticator<NicknameAuthenticator, ChatSession>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets(new WebSocketOptions { AllowedOrigins = { "https://localhost:5001" } });
app.MapDarkWs("/ws");

app.Run();
```

## Браузер {#browser}

```ts
import DarkWs, { ErrorResponse } from "darkws";

type ChatMessage = { author: string; text: string; sentAt: string };
type Broadcast = { id: "@"; action: string; data?: unknown };

const name = prompt("Your nickname")?.trim() || "guest";
const list = document.querySelector("#messages")!;
const form = document.querySelector<HTMLFormElement>("#send")!;
const input = form.querySelector("input")!;

// The query is built again for every socket, so reconnects authenticate too.
const chat = new DarkWs({
  secure: location.protocol === "https:",
  path: "/ws",
  query: () => ({ token: name }),
});

function line(text: string) {
  const item = document.createElement("li");
  item.textContent = text;
  list.append(item);
}

chat.on("message", message => {
  const { action, data } = message as Broadcast;
  if (action === "chat:message") {
    const m = data as ChatMessage;
    line(`${m.author}: ${m.text}`);
  } else if (action === "chat:joined") {
    line(`${(data as { name: string }).name} joined`);
  } else if (action === "chat:left") {
    line(`${(data as { name: string }).name} left`);
  }
});

// "open" fires on the first connection and after every reconnect.
chat.on("open", async () => {
  try {
    const history = await chat.request<ChatMessage[]>("chat:history");
    list.replaceChildren();
    history.forEach(m => line(`${m.author}: ${m.text}`));
  } catch (error) {
    if (error instanceof ErrorResponse && error.message === "darkws:error:authorization-required") {
      chat.close();
      alert("This nickname was rejected.");
    }
  }
});

form.addEventListener("submit", async event => {
  event.preventDefault();
  await chat.request("chat:send", { text: input.value });
  input.value = "";
});

chat.connect();
```

```html
<ul id="messages"></ul>
<form id="send"><input autocomplete="off" required><button>Send</button></form>
```

Событие `message` получает каждую рассылку в виде полного конверта
`{ id: "@", action, data? }`, поэтому страница сама разбирает `action`.

`open` отражает состояние транспорта, а не сессии: отклонённый ник всё равно открывает
сокет, и первый защищённый запрос завершается ошибкой
`darkws:error:authorization-required`. Вместо query-параметра можно также
подключиться анонимно и вызвать `await chat.authenticate(name)` в обработчике `open`
перед запросом истории; после переподключения клиент этот вызов не повторяет.

Перезагрузка истории при каждом `open` покрывает рассылки, пропущенные во время разрыва соединения.
`textContent` не даёт интерпретировать пользовательский текст как HTML.

## Тест {#test}

В DarkWS 4.x нет пакета с тестовым хостом. Тестируйте `ChatHistory` и аутентификатор
как обычные классы, а работу через сокет покрывайте интеграционным тестом против
запущенного экземпляра приложения, например с помощью [.NET-клиента](../clients/dotnet.md).

## Масштабирование {#scaling-out}

Чтобы запустить несколько экземпляров, добавьте [Redis backplane](../server/redis.md)
(`services.AddDarkWsRedis(channel)`) и перенесите историю в общее хранилище. Код
обработчика, middleware и браузера не меняется.
