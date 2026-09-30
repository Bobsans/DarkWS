---
title: Создаём чат
---

# Создаём чат

В этом руководстве создаётся небольшая чат-комната: пользователи выбирают ник, видят недавнюю историю,
отправляют сообщения и видят, кто входит и выходит. Используются типизированные сессии, группы,
рассылки, хуки соединения, браузерный клиент и тест.

## Сессия и аутентификатор {#session-and-authenticator}

Каждое соединение вступает в группу `chat`. Для демо токен — это просто
ник; настоящее приложение проверяет здесь токен.

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
        await PublishAsync(BroadcastTarget.Group("chat"), "chat:message", message);
        return Ok();
    }
}

public sealed record SendInput(string Text);
```

Отправитель тоже получает `chat:message`, поэтому все клиенты отображают сообщения одинаково.
Чтобы вместо этого отображать сообщение отправителя локально, добавьте к цели
`.ExceptConnection(Connection.Id)`.

## Присутствие {#presence}

Хуки соединения сообщают о входе и выходе:

```csharp
public sealed class PresenceHooks(IBroadcaster broadcaster) : DarkWsConnectionHooks {
    public override Task OnOpenAsync(IDarkWsContextAccessor context) =>
        Announce(context, context.Session as ChatSession, "chat:joined");

    public override async Task OnAuthenticatedAsync(IDarkWsContextAccessor context, IDarkWsSession? previousSession) {
        if (previousSession?.Id == context.Session?.Id) return;
        await Announce(context, previousSession as ChatSession, "chat:left");
        await Announce(context, context.Session as ChatSession, "chat:joined");
    }

    public override Task OnCloseAsync(IDarkWsContextAccessor context) =>
        Announce(context, context.Session as ChatSession, "chat:left");

    private Task Announce(IDarkWsContextAccessor context, ChatSession? session, string action) =>
        session is not null
            ? broadcaster.PublishAsync(
                BroadcastTarget.Group("chat").ExceptConnection(context.Connection.Id),
                action, new { session.Name }, context.ConnectionAborted)
            : Task.CompletedTask;
}
```

Присутствие учитывает и аутентификацию при upgrade, и последующий браузерный
`authenticationToken`. Logout объявляет уход прежней сессии, замена — уход перед
приходом новой. Анонимные подключения не порождают событий, повторное использование
той же сессии не объявляет второй приход.

## Program {#program}

```csharp
using DarkWS;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ChatHistory>();
builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<ChatHandler>()
    .AddAuthenticator<NicknameAuthenticator, ChatSession>()
    .AddConnectionHooks<PresenceHooks>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets(new WebSocketOptions { AllowedOrigins = { "https://localhost:5001" } });
app.MapDarkWs("/ws");

app.Run();
```

## Браузер {#browser}

```ts
import DarkWs from "darkws";

type ChatMessage = { author: string; text: string; sentAt: string };

const name = prompt("Your nickname")?.trim() || "guest";
const list = document.querySelector("#messages")!;
const form = document.querySelector<HTMLFormElement>("#send")!;
const input = form.querySelector("input")!;

const chat = new DarkWs({
  secure: location.protocol === "https:",
  path: "/ws",
  authenticationToken: () => name,
});

function line(text: string) {
  const item = document.createElement("li");
  item.textContent = text;
  list.append(item);
}

chat.onAction<ChatMessage>("chat:message", m => line(`${m.author}: ${m.text}`));
chat.onAction<{ name: string }>("chat:joined", p => line(`${p.name} joined`));
chat.onAction<{ name: string }>("chat:left", p => line(`${p.name} left`));

// "open" fires after authentication, on the first connection and after every reconnect.
chat.on("open", async event => {
  if (!chat.isCurrentSocket(event)) return;
  const history = await chat.request<ChatMessage[]>("chat:history");
  list.replaceChildren();
  history.forEach(m => line(`${m.author}: ${m.text}`));
});

chat.on("sessionRestoreFailed", () => {
  chat.close();
  alert("This nickname was rejected.");
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
<form id="send"><input aria-label="Message" autocomplete="off" required><button>Send</button></form>
```

Перезагрузка истории при каждом `open` покрывает рассылки, пропущенные во время разрыва соединения.
`textContent` не даёт интерпретировать пользовательский текст как HTML.

## Тест {#test}

```csharp
[Test]
public async Task SendBroadcastsToTheRoom() {
    await using var host = new DarkWsTestHost(builder => {
        builder.Services.AddSingleton<ChatHistory>();
        builder.AddHandlersFromAssemblyContaining<ChatHandler>();
    });
    var alice = host.CreateConnection(Session("alice"));
    var bob = host.CreateConnection(Session("bob"));

    await host.InvokeAsync(alice, "chat:send",
        JsonSerializer.SerializeToElement(new { text = "hi" }), requestId: "1");

    Assert.That(host.Broadcasts.Single().Action, Is.EqualTo("chat:message"));
    using var received = JsonDocument.Parse(bob.SentMessages.Single());
    Assert.That(received.RootElement.GetProperty("data").GetProperty("text").GetString(), Is.EqualTo("hi"));
}

private static ChatSession Session(string name) => new(
    name, new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "test")), name);
```

## Масштабирование {#scaling-out}

Чтобы запустить несколько экземпляров, добавьте [Redis backplane](../server/redis.md) и перенесите
историю в общее хранилище. Код обработчика, хуков и браузера не меняется.
