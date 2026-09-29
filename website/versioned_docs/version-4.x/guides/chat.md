---
title: Build a chat
---

# Build a chat

This guide builds a small chat room: users pick a nickname, see recent history,
send messages, and see who joins and leaves. It uses typed sessions, groups,
broadcasts, connection middleware, and the browser client.

## Session and authenticator

Every connection joins the `chat` group. For the demo, the token is simply the
nickname, sent in the `token` query parameter of the upgrade request; a real
application validates a token here. When the authenticator returns `null`, the
connection is still accepted, but without a session.

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

The identity needs an authentication type (`"nickname"` here): actions without
`[AllowAnonymous]` require a session whose user is authenticated.

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
        await BroadcastToGroupAsync("chat", "chat:message", message);
        return Ok();
    }
}

public sealed record SendInput(string Text);
```

A group broadcast reaches every member, including the sender, so every client
renders messages the same way. DarkWS 4.x cannot exclude a recipient from a group
broadcast.

## Presence

Connection middleware announces joins and leaves:

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

`AddHandlersFromAssemblyContaining<T>()` registers every non-abstract
`DarkWsMiddleware` subclass in the scanned assembly, so the middleware needs no
separate registration.

`OnOpenAsync` runs after the connection is registered, so the new member also
receives its own `chat:joined`. `OnCloseAsync` runs after the connection is removed,
so the leaving member does not receive `chat:left`. By the time `OnCloseAsync` runs,
the context's `ConnectionAborted` token is already cancelled; the sample therefore
does not pass it to the broadcast.

## Program

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

## Browser

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

The `message` event receives every broadcast as the full envelope
`{ id: "@", action, data? }`, so the page dispatches on `action` itself.

`open` reports the transport, not the session: a rejected nickname still opens the
socket, and the first protected request fails with
`darkws:error:authorization-required`. Instead of the query parameter, you can also
connect anonymously and call `await chat.authenticate(name)` in the `open` handler
before requesting history; the client does not repeat that call after a reconnect.

Reloading history on every `open` covers broadcasts missed while disconnected.
`textContent` keeps user text from being interpreted as HTML.

## Test

DarkWS 4.x has no test host package. Test `ChatHistory` and the authenticator as
ordinary classes, and cover the socket flow with an integration test against a
running instance of the app, for example using the [.NET client](../clients/dotnet.md).

## Scaling out

To run several instances, add the [Redis backplane](../server/redis.md)
(`services.AddDarkWsRedis(channel)`) and move history to a shared store. The handler,
middleware, and browser code do not change.
