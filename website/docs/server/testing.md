---
sidebar_position: 7
title: Testing handlers
---

# Testing handlers

`DarkWS.Testing` runs handlers without an HTTP server, a WebSocket, or Redis. It has
no test framework dependency, so it works with NUnit, xUnit, MSTest, or anything else.

```bash
dotnet add package DarkWS.Testing
```

## Invoke an action through the pipeline

`DarkWsTestHost` builds a service provider with your DarkWS registrations and invokes
registered actions exactly as the server does:

```csharp
using System.Text.Json;
using DarkWS.Testing;
using Microsoft.Extensions.DependencyInjection;

await using var host = new DarkWsTestHost(builder => {
    builder.AddHandlersFromAssemblyContaining<MessageHandler>();
    builder.Services.AddScoped<IMessageStore, FakeMessageStore>();
});

var connection = host.CreateConnection(); // anonymous
await host.InvokeAsync(connection, "message:echo",
    JsonSerializer.SerializeToElement("hello"), requestId: "1");

using var response = JsonDocument.Parse(connection.SentMessages.Single());
Assert.That(response.RootElement.GetProperty("data").GetString(), Is.EqualTo("hello"));
```

`InvokeAsync` uses the real registration, authorization, JSON binding, scope
initializers, action filters, error mapping, serialization, and async scope disposal.
Each invocation creates a fresh message scope. The optional second constructor
argument configures `DarkWsOptions`, for example `options => options.AllowNullPayloads = true`.
`host.Services` is the root service provider.

## Authenticated calls

Pass a session to `CreateConnection`. Its principal must be authenticated
(`Identity.IsAuthenticated` is true):

```csharp
var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "42")], "test"));
var connection = host.CreateConnection(new AppSession("session-1", user, accountId, userId));
```

The session of a test connection is fixed at creation.

## Broadcasts

`host.Broadcasts` records every published broadcast with its target and JSON data.
The real broadcaster also routes them to connections created by the host, so their
`SentMessages` contain both responses and broadcast envelopes (`id` is `"@"`):

```csharp
var sender = host.CreateConnection(aliceSession);
var receiver = host.CreateConnection(bobSession); // same account group

await host.InvokeAsync(sender, "message:send",
    JsonSerializer.SerializeToElement(new { text = "hi" }), requestId: "1");

Assert.That(host.Broadcasts.Single().Action, Is.EqualTo("message:created"));
Assert.That(receiver.SentMessages, Has.Count.EqualTo(1));
```

Create several connections with different sessions and groups to verify targeting.
Captured byte arrays are copies; treat them as read-only.

## Unit-test a handler directly

To call a handler method without the dispatcher, initialize its context from a scope:

```csharp
await using var scope = host.CreateScope(connection);
var handler = new MessageHandler(new FakeMessageStore());
scope.Initialize(handler);

var result = handler.Echo("hello");
await result.WriteResultAsync(new ResponseContext(connection, "direct", new DarkWsOptions()));
```

`scope.Services` resolves constructor dependencies and registered handlers. Direct
initialization sets the connection, session, cancellation, services, and broadcaster,
but skips registration, authorization, scope initializers, and filters, and the action
metadata is null. Use `InvokeAsync` when those checks matter. Keep the scope alive
until you have inspected or written the result.

## What the host does not cover

The host always uses an isolated in-memory backplane, even if your configuration
registers Redis. It does not run hosted services, HTTP middleware, authentication
exchanges, connection lifecycle hooks, transport queues and timeouts, or frame
limits. Cover those with integration tests against a real server, for example with
`WebApplicationFactory` and `DarkWS.Client`.

Configure recipients before invoking concurrent actions, and await all invocations
and dispose manual scopes before disposing the host.
