# DarkWS.Testing

Test DarkWS handlers without an HTTP server, WebSocket, Redis, or test-framework dependency.
Targets .NET 8, 9, and 10. Reference this package from your test project.

```csharp
await using var host = new DarkWsTestHost(builder => {
    builder.AddHandlersFromAssemblyContaining<MyHandler>();
    builder.Services.AddScoped<IMyService, FakeMyService>();
});
var connection = host.CreateConnection(); // Anonymous; supply IDarkWsSession for authenticated actions.
await host.InvokeAsync(connection, "my:echo", JsonSerializer.SerializeToElement("hello"), requestId: "1");
using var response = JsonDocument.Parse(connection.SentMessages.Single());
Assert.That(response.RootElement.GetProperty("data").GetString(), Is.EqualTo("hello"));
```

Use `DarkWS.Testing`, `System.Text.Json`, and `Microsoft.Extensions.DependencyInjection` imports.
The assertion above uses NUnit; the helpers work with any test framework.
Registration, authenticated/anonymous authorization, configured JSON binding, scope initializers,
action filters, error mapping, serialization, and async scope disposal use the server implementation.
An authenticated session needs a principal whose `Identity.IsAuthenticated` is true.
Each invocation creates a fresh message scope; response serialization finishes before its disposal.

`host.Broadcasts` captures every published broadcast with its target and JSON data.
The real broadcaster routes broadcasts to fake recipients created by this host;
their `SentMessages` contain both responses and broadcast envelopes (`id = "@"`).
Create multiple connections with session/group memberships to verify targeted delivery.
Captured byte arrays are copies of send buffers. Treat returned arrays as read-only.

For direct unit tests, initialize an existing handler without the dispatcher:

```csharp
await using var scope = host.CreateScope(connection);
var handler = new MyHandler(new FakeMyService());
scope.Initialize(handler);
var result = handler.Echo("hello");
await result.WriteResultAsync(new ResponseContext(connection, "direct", new DarkWsOptions()));
```

`scope.Services` also resolves constructor dependencies and registered handlers.
Direct initialization sets connection, session, cancellation, services, and broadcaster context;
it deliberately skips action registration, authorization, scope initializers, and filters.
The action metadata is null. Use `InvokeAsync` when these pipeline checks matter.
Keep the scope alive until the result has been inspected or written.

The host owns its service provider and connections and always substitutes an isolated in-memory
backplane, even if application configuration registers Redis. Hosted services, HTTP middleware,
authentication exchanges, connection lifecycle hooks, transport queues/timeouts, and frame limits
are not run. Cover those with real WebSocket integration tests.
Connection sessions are fixed at creation. Configure recipients before invoking concurrent actions,
await all invocations and dispose manual scopes before disposing the host.
