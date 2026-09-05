# DarkWS

ASP.NET Core WebSocket request/response library with typed sessions and an
in-memory broadcast backplane.

```csharp
builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<Program>()
    .AddAuthenticator<AppAuthenticator, AppSession>();

app.UseWebSockets();
app.MapDarkWs("/ws");
```

```csharp
public sealed record AppSession(
    string Id,
    ClaimsPrincipal User,
    Guid AccountId
) : IDarkWsSession {
    public IReadOnlyCollection<string> Groups => [$"account:{AccountId}"];
}

[Handler("message")]
public sealed class MessageHandler : HandlerBase<AppSession> {
    [Action("send")]
    public async Task<IResponse> SendAsync(MessageInput input) {
        await BroadcastToGroupAsync($"account:{Session.AccountId}", "message:created", input);
        return Ok();
    }
}
```

Handlers require an authenticated session by default. Add `[AllowAnonymous]`
to public handlers or actions. Register `AddSession` and call `UseSession`
before `MapDarkWs` when handlers need ASP.NET `ISession`.
