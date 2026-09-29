---
title: Build a chat
---

# Build a chat

This guide builds a small chat room: users pick a nickname, see recent history,
send messages, and see who joins and leaves. It uses typed sessions, groups,
broadcasts, connection hooks, the browser client, and a test.

## Session and authenticator

Every connection joins the `chat` group. For the demo, the token is simply the
nickname; a real application validates a token here.

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

## History

A singleton keeps the last 50 messages:

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

With several server instances, keep history in a shared store such as a database.

## Handler

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

The sender also receives `chat:message`, so every client renders messages the same
way. To render the sender's message locally instead, add
`.ExceptConnection(Connection.Id)` to the target.

## Presence

Connection hooks announce joins and leaves:

```csharp
public sealed class PresenceHooks(IBroadcaster broadcaster) : DarkWsConnectionHooks {
    public override Task OnOpenAsync(IDarkWsContextAccessor context) =>
        Announce(context, "chat:joined");

    public override Task OnCloseAsync(IDarkWsContextAccessor context) =>
        Announce(context, "chat:left");

    private Task Announce(IDarkWsContextAccessor context, string action) =>
        context.Session is ChatSession session
            ? broadcaster.PublishAsync(
                BroadcastTarget.Group("chat").ExceptConnection(context.Connection.Id),
                action, new { session.Name }, context.ConnectionAborted)
            : Task.CompletedTask;
}
```

## Program

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

## Browser

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
<form id="send"><input autocomplete="off" required><button>Send</button></form>
```

Reloading history on every `open` covers broadcasts missed while disconnected.
`textContent` keeps user text from being interpreted as HTML.

## Test

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

## Scaling out

To run several instances, add the [Redis backplane](../server/redis.md) and move
history to a shared store. The handler, hooks, and browser code do not change.
