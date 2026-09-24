# DarkWS.Client.DependencyInjection

Optional Microsoft DI integration for DarkWS.Client on .NET 8, 9, and 10.
Install with `dotnet add package DarkWS.Client.DependencyInjection`.
No ASP.NET framework reference or Generic Host is required by the package.

```csharp
using DarkWS.Client;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddDarkWsClient(options => {
    options.Endpoint = new Uri("wss://example.com/ws");
});
services.AddTransient<Calculator>();

await using var provider = services.BuildServiceProvider();
var result = await provider.GetRequiredService<Calculator>().SumAsync(10, 20);

public sealed class Calculator(IDarkWsClient client) {
    public Task<int> SumAsync(int left, int right) =>
        client.RequestAsync<int>("math:sum", new { left, right });
}
```

The example assumes a server action returning an integer. The application needs
`Microsoft.Extensions.DependencyInjection` to build a container, or uses its
existing host. This package only references DI abstractions and the core client.

`AddDarkWsClient` registers one lazy singleton **IDarkWsClient**. Resolving it does
not connect; the first request or `ConnectAsync` opens the socket. The container
owns disposal. Injected consumers do not dispose the shared client; they own and
dispose their subscriptions. Duplicate/conflicting registrations throw before
mutating the collection. No hosted service or private provider is created.

Use application services for configuration or token refresh:

```csharp
services.AddDarkWsClient((provider, options) => {
    options.Endpoint = new Uri("wss://example.com/ws");
    var tokens = provider.GetRequiredService<TokenStore>();
    options.AuthenticationTokenProvider = ct => tokens.GetAccessTokenAsync(ct);
});
```

`TokenStore` must have a lifetime suitable for a singleton and return
`ValueTask<string?>`. Do not capture scoped services in singleton callbacks.
Token storage, refresh, and logout cleanup belong to the application.

## Separate user sessions

A singleton is suitable for one shared server identity. Use standard DI
registration for separate identities per application scope:

```csharp
services.AddScoped<IDarkWsClient>(provider => {
    var tokens = provider.GetRequiredService<UserTokenStore>();
    return new DarkWsClient(new DarkWsClientOptions {
        Endpoint = new Uri("wss://example.com/ws"),
        AuthenticationTokenProvider = ct => tokens.GetAccessTokenAsync(ct)
    });
});
```

Match the scope lifetime to the session: an ordinary HTTP request scope ends with
that request. Do not combine this unkeyed registration with `AddDarkWsClient`.
Keyed registrations (which `AddDarkWsClient` does not treat as duplicates) or
application-owned instances cover multiple endpoints; there is no custom
named-client framework.

See the core package README for cancellation, reconnect, notification delivery,
timeouts, and authentication rules.
