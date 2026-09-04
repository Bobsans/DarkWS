# DarkWS Platform Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build reusable ASP.NET Core and browser DarkWS packages with typed sessions, an in-memory default backplane, and an optional Redis backplane.

**Architecture:** `DarkBoy.DarkWS` owns the wire protocol, ASP.NET endpoint, handler dispatch, session context, local connections, broadcasting, and default in-memory backplane. `DarkBoy.DarkWS.Redis` replaces only the backplane. `@darkboy/darkws` is a dependency-free ESM browser client that implements the same wire contract.

**Tech Stack:** C# 12, .NET 8/9/10, ASP.NET Core, NUnit, StackExchange.Redis 2.13.17, Testcontainers.Redis 4.14.0, TypeScript 6.0.3, Vitest 4.1.11

**Spec:** `docs/superpowers/specs/2026-09-04-darkws-platform-design.md`

## Global Constraints

- Keep Fixdigital read-only; copy behavior, never business types or services.
- Keep all .NET public identities under `DarkBoy.DarkWS`.
- Keep Redis in the separate `DarkBoy.DarkWS.Redis` NuGet package.
- Keep the core package dependency-free beyond `Microsoft.AspNetCore.App`.
- Use the in-memory backplane by default.
- Publish the browser client as `@darkboy/darkws` with no runtime dependencies.
- Preserve wire fields exactly: `id`, `action`, `payload`, `data`, and `error`.
- Do not add a high-level frontend adapter in this change.
- Do not add tenant, user, localization, notification, or Sentry concepts.
- Write tests before their production changes, but run all checks once in Task 6.
- Do not commit, push, publish, or configure a remote.

---

### Task 1: Define protocol, session, and request-context contracts

**Files:**
- Create: `DarkBoy.DarkWS/Abstractions/IDarkWsSession.cs`
- Create: `DarkBoy.DarkWS/Abstractions/IDarkWsContextAccessor.cs`
- Create: `DarkBoy.DarkWS/Abstractions/IDarkWsAuthenticator.cs`
- Create: `DarkBoy.DarkWS/DarkWsContextAccessor.cs`
- Create: `DarkBoy.DarkWS/AspNetDarkWsSession.cs`
- Modify: `DarkBoy.DarkWS/Messages.cs`
- Modify: `DarkBoy.DarkWS/HandlerBase.cs`
- Delete: `DarkBoy.DarkWS/Abstractions/IWebSocketCredentials.cs`
- Delete: `DarkBoy.DarkWS/WebSocketCredentialsAccessor.cs`
- Test: `DarkBoy.DarkWS.Test/SessionContextTests.cs`

**Interfaces:**
- Consumes: ASP.NET `HttpContext`, optional `ISession`, existing `IWebSocketConnection`
- Produces: `IDarkWsSession`, `IDarkWsContextAccessor`, `IDarkWsAuthenticator`, `HandlerBase<TSession>`, and the stable request wire model

- [ ] **Step 1: Write session/context tests**

Add tests that create an authenticated `AppSession`, initialize one request
context, and assert handler and scoped-service access to the same objects:

```csharp
public sealed record AppSession(
    string Id,
    ClaimsPrincipal User,
    int TenantId
) : IDarkWsSession {
    public IReadOnlyCollection<string> Groups => [$"tenant:{TenantId}"];
}

[Test]
public void HandlerAndScopedAccessorExposeSameSession() {
    Assert.That(handler.SessionValue, Is.SameAs(session));
    Assert.That(accessor.Session, Is.SameAs(session));
    Assert.That(accessor.HttpContext, Is.SameAs(httpContext));
    Assert.That(accessor.Connection, Is.SameAs(connection));
}

[Test]
public void AspNetSessionIsNullWhenSessionMiddlewareIsNotConfigured() {
    Assert.That(accessor.AspNetSession, Is.Null);
}
```

Do not run tests yet.

- [ ] **Step 2: Replace credentials with a typed session contract**

Create:

```csharp
public interface IDarkWsSession {
    string Id { get; }
    ClaimsPrincipal User { get; }
    IReadOnlyCollection<string> Groups { get; }
}
```

Create `AspNetDarkWsSession` for hosts that use ASP.NET authentication without a
custom authenticator. Its id comes from the `sid` claim, then ASP.NET
`ISession.Id`, then a generated GUID. Its groups are empty.

- [ ] **Step 3: Add the scoped request context**

Create this public read-only contract and an internal-set implementation:

```csharp
public interface IDarkWsContextAccessor {
    IDarkWsSession? Session { get; }
    HttpContext HttpContext { get; }
    ISession? AspNetSession { get; }
    IWebSocketConnection Connection { get; }
    CancellationToken ConnectionAborted { get; }
}
```

Resolve optional ASP.NET session through `ISessionFeature` so accessing the
property does not throw when the host omitted session middleware.

- [ ] **Step 4: Define authentication**

Create:

```csharp
public interface IDarkWsAuthenticator {
    ValueTask<IDarkWsSession?> AuthenticateAsync(
        HttpContext context,
        string? token,
        CancellationToken cancellationToken
    );
}
```

The default authenticator returns `AspNetDarkWsSession` only when
`HttpContext.User.Identity.IsAuthenticated` is true. A custom authenticator may
return any `IDarkWsSession` implementation.

- [ ] **Step 5: Make handlers session-aware**

Keep non-generic `HandlerBase` for anonymous handlers. Add:

```csharp
public abstract class HandlerBase<TSession> : HandlerBase
    where TSession : class, IDarkWsSession {
    protected new TSession Session =>
        base.Session as TSession
        ?? throw new InvalidOperationException(
            $"Expected session type {typeof(TSession).FullName}"
        );
}
```

`HandlerBase` exposes protected `Session`, `HttpContext`, `AspNetSession`,
`Connection`, and `ConnectionAborted` from its initialized request context.

- [ ] **Step 6: Correct the request wire model**

Change only the request payload property:

```csharp
public record InputMessage(string Id, string Action, JsonElement? Payload);
```

Keep response and broadcast field shapes unchanged.

### Task 2: Replace global dispatch with explicit ASP.NET registration

**Files:**
- Create: `DarkBoy.DarkWS/DarkWsBuilder.cs`
- Create: `DarkBoy.DarkWS/DarkWsActionRegistry.cs`
- Create: `DarkBoy.DarkWS/DarkWsMiddleware.cs`
- Create: `DarkBoy.DarkWS/Abstractions/IDarkWsScopeInitializer.cs`
- Modify: `DarkBoy.DarkWS/Configuration.cs`
- Modify: `DarkBoy.DarkWS/ActionDescriptor.cs`
- Modify: `DarkBoy.DarkWS/Attributes.cs`
- Modify: `DarkBoy.DarkWS/WebSocketHandler.cs`
- Modify: `DarkBoy.DarkWS/WebSocketConnection.cs`
- Modify: `DarkBoy.DarkWS/Abstractions/IWebSocketConnection.cs`
- Delete: `DarkBoy.DarkWS/DarkWsOptionsBuilder.cs`
- Delete: `DarkBoy.DarkWS/DefaultScopeInitializer.cs`
- Delete: `DarkBoy.DarkWS/Abstractions/IScopeInitializer.cs`
- Test: `DarkBoy.DarkWS.Test/DispatchTests.cs`
- Test: `DarkBoy.DarkWS.Test/AuthenticationTests.cs`

**Interfaces:**
- Consumes: Task 1 session/context contracts and existing `[Handler]`/`[Action]` methods
- Produces: `AddDarkWs`, `AddHandlersFromAssemblyContaining<T>`, `AddAuthenticator<TAuthenticator,TSession>`, `MapDarkWs`, per-request async scopes, lifecycle hooks, and non-static action registration

- [ ] **Step 1: Write dispatcher and authentication tests**

Cover these complete behaviors:

```csharp
[Test]
public async Task AnonymousActionRunsWithoutSessionAsync() { }

[Test]
public async Task ActionRequiresAuthenticationByDefaultAsync() { }

[Test]
public async Task AuthMessageReplacesSessionAndHttpPrincipalAsync() { }

[Test]
public async Task EachMessageUsesANewAsyncScopeAsync() { }

[Test]
public void DuplicateActionRegistrationFailsAtStartup() { }

[Test]
public async Task UnhandledExceptionDoesNotLeakItsMessageAsync() { }
```

Use real `TestServer` WebSockets for protocol behavior. Use a test authenticator
that maps tokens to `AppSession`. Do not run tests yet.

- [ ] **Step 2: Add explicit builder registration**

`AddDarkWs()` returns `DarkWsBuilder`. The builder owns `IServiceCollection` and
one `DarkWsActionRegistry`. Add:

```csharp
public DarkWsBuilder AddHandlersFromAssemblyContaining<T>();

public DarkWsBuilder AddAuthenticator<TAuthenticator, TSession>()
    where TAuthenticator : class, IDarkWsAuthenticator
    where TSession : class, IDarkWsSession;
```

Handler registration is scoped. Connection storage, broadcaster, action
registry, backplane, and backplane listener are singleton. Request context,
authenticator, scope initializers, middleware, and handler dispatcher follow
their required scoped or singleton lifetimes without resolving scoped services
from the root provider.

- [ ] **Step 3: Build a DI-owned action registry**

Move descriptors out of `WebSocketHandler.HandleMap`. Register exact action
keys from `[Handler("group")]` and `[Action("name")]`. Store whether class or
method has `[AllowAnonymous]`. Reject duplicates with the duplicate key in the
exception.

Read assembly types through:

```csharp
private static IEnumerable<Type> GetLoadableTypes(Assembly assembly) {
    try {
        return assembly.GetTypes();
    } catch (ReflectionTypeLoadException error) {
        return error.Types.OfType<Type>();
    }
}
```

- [ ] **Step 4: Add connection lifecycle middleware**

Create a base class with no-op hooks:

```csharp
public abstract class DarkWsMiddleware {
    public virtual Task OnOpenAsync(IDarkWsContextAccessor context) =>
        Task.CompletedTask;

    public virtual Task OnAuthenticatedAsync(
        IDarkWsContextAccessor context,
        IDarkWsSession? previousSession
    ) => Task.CompletedTask;

    public virtual Task OnCloseAsync(IDarkWsContextAccessor context) =>
        Task.CompletedTask;
}
```

Register derived middleware from the same explicit assemblies as handlers.

- [ ] **Step 5: Add the application scope hook**

Create a no-op-by-default extension point called after context initialization
and before handler resolution:

```csharp
public interface IDarkWsScopeInitializer {
    ValueTask InitializeAsync(
        IServiceProvider scopedServices,
        IDarkWsContextAccessor context,
        CancellationToken cancellationToken
    );
}
```

Resolve all registered initializers from the message scope. This replaces the
old initializer that created the scope itself.

- [ ] **Step 6: Map the WebSocket endpoint**

Implement:

```csharp
public static IEndpointConventionBuilder MapDarkWs(
    this IEndpointRouteBuilder endpoints,
    string pattern = "/ws"
);
```

Link cancellation to `HttpContext.RequestAborted` and
`IHostApplicationLifetime.ApplicationStopping`. Authenticate the initial token
from the configured query parameter, accept the socket, and pass one connection
to `WebSocketHandler`.

- [ ] **Step 7: Dispatch each message in a new async scope**

For each JSON request:

1. Resolve action descriptor.
2. Enforce authentication unless `[AllowAnonymous]` applies.
3. Create `AsyncServiceScope`.
4. Initialize scoped `IDarkWsContextAccessor`.
5. Run `IDarkWsScopeInitializer` instances.
6. Resolve and initialize handler.
7. Deserialize `message.Payload`.
8. Invoke sync or async action.
9. Write result.

Track background tasks. On close, wait at most the configured shutdown timeout,
then close, remove, and dispose the connection.

- [ ] **Step 8: Support re-authentication and safe errors**

Handle `auth:<token>` before JSON parsing. On success, atomically replace the
connection session, set `HttpContext.User`, update the request context, and run
`OnAuthenticatedAsync`.

Return configured stable values for invalid action, invalid request,
authorization required, and request failed. Log full exceptions with
`ILogger<WebSocketHandler>`; never send exception text or stack traces.

### Task 3: Add the in-memory backplane and generic routing

**Files:**
- Create: `DarkBoy.DarkWS/Abstractions/IDarkWsBackplane.cs`
- Create: `DarkBoy.DarkWS/DarkWsBroadcast.cs`
- Create: `DarkBoy.DarkWS/InMemoryDarkWsBackplane.cs`
- Create: `DarkBoy.DarkWS/DarkWsBackplaneHostedService.cs`
- Modify: `DarkBoy.DarkWS/ConnectionStorage.cs`
- Modify: `DarkBoy.DarkWS/Broadcaster.cs`
- Modify: `DarkBoy.DarkWS/Abstractions/IBroadcaster.cs`
- Modify: `DarkBoy.DarkWS/DarkWsOptions.cs`
- Modify: `DarkBoy.DarkWS/WebSocketConnection.cs`
- Test: `DarkBoy.DarkWS.Test/BroadcastTests.cs`
- Test: `DarkBoy.DarkWS.Test/WebSocketConnectionTests.cs`

**Interfaces:**
- Consumes: `IDarkWsSession.Id`, `IDarkWsSession.Groups`, connection storage, stable broadcast wire shape
- Produces: default local delivery and target-independent broadcast methods usable unchanged by the Redis package

- [ ] **Step 1: Write routing and send-safety tests**

Cover exact outcomes:

```csharp
[Test]
public async Task BroadcastAllReachesEveryLocalConnectionAsync() { }

[Test]
public async Task BroadcastToConnectionReachesOnlyMatchingConnectionAsync() { }

[Test]
public async Task BroadcastToSessionReachesAllConnectionsInSessionAsync() { }

[Test]
public async Task BroadcastToGroupReachesAllGroupMembersAsync() { }

[Test]
public async Task ConcurrentSendsNeverOverlapOnOneSocketAsync() { }

[Test]
public async Task TimedOutBroadcastAbortsConnectionAsync() { }
```

Do not run tests yet.

- [ ] **Step 2: Define backplane messages**

Create the spec-defined `DarkWsTarget` enum and `DarkWsBroadcast` record. Define:

```csharp
public interface IDarkWsBackplane {
    ValueTask PublishAsync(
        DarkWsBroadcast message,
        CancellationToken cancellationToken = default
    );

    ValueTask SubscribeAsync(
        Func<DarkWsBroadcast, CancellationToken, ValueTask> listener,
        CancellationToken cancellationToken = default
    );

    ValueTask UnsubscribeAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Implement default in-memory delivery**

`InMemoryDarkWsBackplane` stores one listener. `PublishAsync` invokes it once.
Subscription replacement and unsubscription are safe under concurrent startup
and shutdown. No channels, serialization, or retry logic belongs here.

- [ ] **Step 4: Make connection storage concurrent and routable**

Replace `List<IWebSocketConnection>` with
`ConcurrentDictionary<string, IWebSocketConnection>`. Add snapshot-returning
queries for connection id, session id, and exact ordinal group string.

- [ ] **Step 5: Route every broadcast through the backplane**

Expose:

```csharp
Task BroadcastAsync<T>(string action, T? data, CancellationToken token = default);
Task BroadcastToConnectionAsync<T>(string id, string action, T? data, CancellationToken token = default);
Task BroadcastToSessionAsync<T>(string id, string action, T? data, CancellationToken token = default);
Task BroadcastToGroupAsync<T>(string group, string action, T? data, CancellationToken token = default);
```

Serialize `data` to `JsonElement` with the configured JSON options before
publishing. The hosted listener selects local connections and sends the normal
`id: "@"` broadcast response. Receiving never republishes.

- [ ] **Step 6: Serialize connection writes and bound broadcasts**

Guard all sends on one connection with `SemaphoreSlim`. Add options:

```csharp
public TimeSpan BroadcastSendTimeout { get; set; } = TimeSpan.FromSeconds(10);
public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(10);
public string AuthenticationQueryParameter { get; set; } = "token";
public string InvalidActionError { get; set; } = "darkws:error:invalid-action";
public string InvalidRequestError { get; set; } = "darkws:error:invalid-request";
public string AuthorizationRequiredError { get; set; } = "darkws:error:authorization-required";
public string RequestFailedError { get; set; } = "darkws:error:request-failed";
```

On broadcast timeout, log and abort the socket. Dispose the send lock with the
connection.

### Task 4: Add the optional Redis backplane package

**Files:**
- Create: `DarkBoy.DarkWS.Redis/DarkBoy.DarkWS.Redis.csproj`
- Create: `DarkBoy.DarkWS.Redis/RedisDarkWsBackplane.cs`
- Create: `DarkBoy.DarkWS.Redis/RedisConfiguration.cs`
- Create: `DarkBoy.DarkWS.Redis/RedisDarkWsOptions.cs`
- Create: `DarkBoy.DarkWS.Redis/readme.md`
- Create: `DarkBoy.DarkWS.Redis.Test/DarkBoy.DarkWS.Redis.Test.csproj`
- Create: `DarkBoy.DarkWS.Redis.Test/RedisBackplaneTests.cs`
- Modify: `DarkBoy.DarkWS.sln`

**Interfaces:**
- Consumes: Task 3 `IDarkWsBackplane` and `DarkWsBroadcast`; host-provided `IConnectionMultiplexer`
- Produces: `AddDarkWsRedis(string channel)` and cross-instance broadcast delivery

- [ ] **Step 1: Write the two-instance Redis integration test**

Use one disposable Redis container and two independent service providers. Give
each provider one local connection. Register the same explicit channel, publish
from provider A, and assert both A and B receive exactly one copy. Publish a
session/group target and assert only matching connections receive it.

```csharp
[Test]
public async Task BroadcastCrossesInstancesExactlyOnceAsync() { }

[Test]
public async Task TargetedBroadcastFiltersConnectionsOnEachInstanceAsync() { }
```

Do not run tests yet.

- [ ] **Step 2: Create package projects**

Target `net8.0;net9.0;net10.0`. The Redis package references
`DarkBoy.DarkWS` and `StackExchange.Redis` version `2.13.17`. The test project
references both projects, NUnit packages matching the existing test project,
and `Testcontainers.Redis` version `4.14.0`.

- [ ] **Step 3: Register an explicit Redis channel**

Implement:

```csharp
public static IServiceCollection AddDarkWsRedis(
    this IServiceCollection services,
    string channel
);
```

Reject null, empty, or whitespace channels. Use `services.Replace` to replace
the default singleton `IDarkWsBackplane`. Resolve the existing
`IConnectionMultiplexer`; never parse or store credentials.

- [ ] **Step 4: Implement publish and subscription lifecycle**

Serialize `DarkWsBroadcast` with the core JSON options. Publish to one literal
Redis channel. Subscribe once, deserialize messages, invoke the local listener,
and catch/log malformed messages without killing the subscription. Unsubscribe
on host shutdown. Never republish received messages.

- [ ] **Step 5: Add both projects to the solution**

Run during implementation setup, not verification:

```powershell
dotnet sln DarkBoy.DarkWS.sln add `
  DarkBoy.DarkWS.Redis\DarkBoy.DarkWS.Redis.csproj `
  DarkBoy.DarkWS.Redis.Test\DarkBoy.DarkWS.Redis.Test.csproj
```

### Task 5: Extract the dependency-free browser package

**Files:**
- Create: `packages/darkws/package.json`
- Create: `packages/darkws/package-lock.json`
- Create: `packages/darkws/tsconfig.json`
- Create: `packages/darkws/README.md`
- Create: `packages/darkws/src/index.ts`
- Create: `packages/darkws/src/dark-ws.ts`
- Create: `packages/darkws/test/dark-ws.test.ts`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: stable Task 1 wire contract and proven low-level behavior from Fixdigital `app/src/core/vendor/darkws.ts`
- Produces: publishable `@darkboy/darkws` ESM package with declarations and zero runtime dependencies

- [ ] **Step 1: Port the low-level tests before implementation**

Move the vendor tests for clean/abnormal reconnect, intentional close, immediate
reconnect, exponential jitter, connection-wait timeout, request timeout,
resolver cleanup, and self-removing listeners. Add:

```ts
it("disposes timers and rejects pending requests", async () => {});
it("sends auth control messages", async () => {});
it("builds an URL without an empty question mark", () => {});
it("delivers broadcast action and data", () => {});
```

Use a local typed `MockWebSocket`. Do not import Vue, aliases, lodash, or
Fixdigital services. Do not run tests yet.

- [ ] **Step 2: Create exact package metadata**

Use:

```json
{
  "name": "@darkboy/darkws",
  "version": "1.0.0",
  "type": "module",
  "license": "MIT",
  "files": ["dist", "README.md"],
  "main": "./dist/index.js",
  "types": "./dist/index.d.ts",
  "exports": {
    ".": {
      "types": "./dist/index.d.ts",
      "import": "./dist/index.js"
    }
  },
  "scripts": {
    "build": "tsc -p tsconfig.json",
    "test": "vitest run"
  },
  "devDependencies": {
    "typescript": "6.0.3",
    "vitest": "4.1.11"
  }
}
```

Generate `package-lock.json` with `npm install --package-lock-only` after all
package files exist.

- [ ] **Step 3: Configure declaration-producing ESM output**

Use `target` and `module` `ES2022`, `moduleResolution` `Bundler`, DOM libs,
strict mode, declarations, source maps, `rootDir: "src"`, and
`outDir: "dist"`. Exclude tests from build output.

- [ ] **Step 4: Implement the dependency-free client**

Port the Fixdigital low-level class and preserve its protocol behavior. Replace:

```ts
randomString(8)       // with crypto.randomUUID()
isFunction(value)     // with typeof value === "function"
window.setTimeout     // with global setTimeout
```

Represent wire payloads as `unknown` and generic type parameters. Register a
request resolver before a response can arrive; remove it on success, server
error, send failure, timeout, socket replacement, close, and dispose.

- [ ] **Step 5: Add terminal disposal and stable exports**

`dispose()` marks the client terminal, clears reconnect/ping timers, closes the
socket, rejects pending requests with `ConnectionClosedError`, and prevents
future reconnects. Export from `src/index.ts`:

```ts
export { default as DarkWs } from "./dark-ws.js";
export {
  ConnectionClosedError,
  ErrorResponse,
  RequestTimeoutError,
} from "./dark-ws.js";
export type { DarkWsEvents, DarkWsOptions } from "./dark-ws.js";
```

Do not add the future high-level adapter or placeholder interfaces.

- [ ] **Step 6: Document the low-level package**

README examples cover connect, request, typed response, broadcast subscription,
authentication, explicit close, and dispose. State browser-only ESM support and
zero runtime dependencies.

### Task 6: Run the single final verification block

**Files:**
- Verify: all solution projects and `packages/darkws`
- Verify: NuGet and npm package contents
- Verify: source repository remains unchanged

**Interfaces:**
- Consumes: completed Tasks 1-5
- Produces: one evidence set for compilation, behavior, packaging, and source isolation

- [ ] **Step 1: Check identity, forbidden coupling, formatting, and solution membership**

Run:

```powershell
rg -n -i 'Fixdigital|DimTim|Sentry|TenantId|FixdigitalUserId' `
  DarkBoy.DarkWS DarkBoy.DarkWS.Redis packages/darkws
rg -n '@/|lodash|vue' packages/darkws/src packages/darkws/test
dotnet sln DarkBoy.DarkWS.sln list
git diff --check
```

Expected: both searches return no matches; solution lists core, core tests,
Redis, and Redis tests; diff check reports nothing.

- [ ] **Step 2: Restore, build, and test every .NET target**

Run directly from the repository root because the local AIHelper runner passes
an extended Windows path that breaks MSBuild wildcard imports:

```powershell
dotnet restore DarkBoy.DarkWS.sln
dotnet build DarkBoy.DarkWS.sln --no-restore
dotnet test DarkBoy.DarkWS.sln --no-build
```

Expected: exit code `0`, zero failed tests for `net8.0`, `net9.0`, and
`net10.0`. Report inherited warnings separately; do not repair unrelated code.

- [ ] **Step 3: Build, test, and inspect the npm package**

Run:

```powershell
npm install
npm test
npm run build
npm pack --dry-run
```

Working directory: `packages/darkws`.

Expected: tests and TypeScript build pass; pack list contains only README,
package metadata, compiled JavaScript, declarations, and source maps. Runtime
dependency count is zero.

- [ ] **Step 4: Pack both NuGet packages**

Run:

```powershell
dotnet pack DarkBoy.DarkWS\DarkBoy.DarkWS.csproj -c Release --no-restore
dotnet pack DarkBoy.DarkWS.Redis\DarkBoy.DarkWS.Redis.csproj -c Release --no-restore
```

Expected: both `.nupkg` files are created with README and required assemblies;
the core package has no Redis dependency, and the Redis package declares both
`DarkBoy.DarkWS` and `StackExchange.Redis`.

- [ ] **Step 5: Verify source isolation and target status**

Run:

```powershell
ah --cwd 'D:\Work\UCO\fixdigital\lms.fixdigital.co.il' --json git status
git status --short
git check-ignore `
  DarkBoy.DarkWS\bin DarkBoy.DarkWS\obj `
  DarkBoy.DarkWS.Redis\bin DarkBoy.DarkWS.Redis\obj `
  packages\darkws\node_modules packages\darkws\dist
```

Expected: Fixdigital remains clean at its original commit, target changes remain
uncommitted, and all generated directories are ignored. Do not commit, push,
publish, or configure a remote.
