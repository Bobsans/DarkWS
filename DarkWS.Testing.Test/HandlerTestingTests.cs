using System.Security.Claims;
using System.Text.Json;
using DarkWS.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DarkWS.Testing.Test;

// This assembly has no InternalsVisibleTo grant from DarkWS or DarkWS.Testing.
public sealed class HandlerTestingTests {
    private static DarkWsTestHost CreateHost(Action<DarkWsOptions>? options = null) => new(builder => {
        builder.AddHandlersFromAssemblyContaining<EchoHandler>()
            .AddScopeInitializer<Initializer>().AddActionFilter<Filter>();
        builder.Services.AddSingleton<Probe>();
        builder.Services.AddScoped<ScopedDependency>();
    }, options);

    [Test]
    public async Task RegisteredActionsUseBindingFiltersFreshScopesAndDeferredResponseSerialization() {
        await using var host = CreateHost(options => options.JsonOptions.PropertyNameCaseInsensitive = true);
        var connection = host.CreateConnection();
        var probe = host.Services.GetRequiredService<Probe>();
        for (var index = 0; index < 2; index++) {
            await host.InvokeAsync(connection, "testing:echo", JsonSerializer.SerializeToElement(new { VALUE = "hello" }), requestId: index.ToString());
            using var response = JsonDocument.Parse(connection.SentMessages[index]);
            Assert.That(response.RootElement.GetProperty("data")[0].GetString(), Is.EqualTo("hello"));
            Assert.That(response.RootElement.GetProperty("id").GetString(), Is.EqualTo(index.ToString()));
        }
        Assert.That(probe.Events, Is.EqualTo(new[] {
            "initialize:testing:echo", "filter:hello", "handler", "serialize", "dispose",
            "initialize:testing:echo", "filter:hello", "handler", "serialize", "dispose"
        }));
        Assert.That(probe.Scopes.Distinct().Count(), Is.EqualTo(2));
    }

    [TestCase("testing:secure", "{}", "auth")]
    [TestCase("missing", "{}", "action")]
    [TestCase("testing:echo", "42", "request")]
    [TestCase("testing:echo", "null", "request")]
    [TestCase("testing:controlled", "null", "domain:error")]
    [TestCase("testing:throws", "null", "failed")]
    [TestCase("testing:blocked", "null", "blocked")]
    public async Task PipelineUsesConfiguredErrorsAndFilterShortCircuit(string action, string json, string expected) {
        await using var host = CreateHost(options => {
            options.AuthorizationRequiredError = "auth";
            options.InvalidActionError = "action";
            options.InvalidRequestError = "request";
            options.RequestFailedError = "failed";
        });
        var connection = host.CreateConnection();
        using var data = JsonDocument.Parse(json);
        await host.InvokeAsync(connection, action, data.RootElement, "error");
        using var response = JsonDocument.Parse(connection.SentMessages.Single());
        Assert.That(response.RootElement.GetProperty("error").GetString(), Is.EqualTo(expected));
        Assert.That(response.RootElement.GetProperty("id").GetString(), Is.EqualTo("error"));
    }

    [Test]
    public async Task SessionMustBeAuthenticatedAndRequestIdsUseRealValidation() {
        await using var host = CreateHost();
        var anonymous = host.CreateConnection(new TestSession("anonymous", [], authenticated: false));
        await host.InvokeAsync(anonymous, "testing:secure");
        using var denied = JsonDocument.Parse(anonymous.SentMessages.Single());
        Assert.That(denied.RootElement.GetProperty("error").GetString(), Is.EqualTo("darkws:error:authorization-required"));
        var session = new TestSession("user", ["group"]);
        var authenticated = host.CreateConnection(session);
        await host.InvokeAsync(authenticated, "testing:secure");
        using var allowed = JsonDocument.Parse(authenticated.SentMessages.Single());
        Assert.That(allowed.RootElement.GetProperty("data").GetString(), Is.EqualTo("user"));
        Assert.That(authenticated.HttpContext.User, Is.SameAs(session.User));
        await host.InvokeAsync(authenticated, "testing:secure", requestId: "@");
        using var invalid = JsonDocument.Parse(authenticated.SentMessages.Last());
        Assert.That(invalid.RootElement.GetProperty("error").GetString(), Is.EqualTo("darkws:error:invalid-request"));
        Assert.That(invalid.RootElement.GetProperty("id").GetString(), Is.Empty);
    }

    [Test]
    public async Task DirectInitializationProvidesContextAndRealTargetedBroadcastsWithoutDispatcher() {
        await using var host = CreateHost();
        var session = new TestSession("same", ["group"]);
        var http = new DefaultHttpContext();
        var connection = host.CreateConnection(session, http);
        var sameSession = host.CreateConnection(session);
        var sameGroup = host.CreateConnection(new TestSession("other", ["group"]));
        var unrelated = host.CreateConnection();
        using var cancellation = new CancellationTokenSource();
        await using var scope = host.CreateScope(connection, cancellation.Token);
        var handler = new DirectHandler();
        scope.Initialize(handler);
        Assert.That(handler.Current, Is.EqualTo((session, http, connection, cancellation.Token, scope.Services)));
        Assert.That(handler.OptionalAspNetSession, Is.Null);
        Assert.That(scope.Services.GetRequiredService<IDarkWsContextAccessor>().Action, Is.Null);
        await handler.Broadcasts();
        Assert.That(host.Broadcasts.Select(message => message.Target), Is.EqualTo(new[] {
            DarkWsTarget.All, DarkWsTarget.All, DarkWsTarget.Connection, DarkWsTarget.Connection,
            DarkWsTarget.Session, DarkWsTarget.Session, DarkWsTarget.Group, DarkWsTarget.Group
        }));
        Assert.That(connection.SentMessages, Has.Count.EqualTo(8));
        Assert.That(sameSession.SentMessages, Has.Count.EqualTo(6));
        Assert.That(sameGroup.SentMessages, Has.Count.EqualTo(4));
        Assert.That(unrelated.SentMessages, Has.Count.EqualTo(2));
        using var empty = JsonDocument.Parse(connection.SentMessages[0]);
        using var explicitNull = JsonDocument.Parse(connection.SentMessages[1]);
        Assert.That(empty.RootElement.TryGetProperty("data", out _), Is.False);
        Assert.That(explicitNull.RootElement.GetProperty("data").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(explicitNull.RootElement.GetProperty("id").GetString(), Is.EqualTo("@"));
        await handler.MissingRecipient();
        Assert.That(host.Broadcasts, Has.Count.EqualTo(9));
        Assert.That(connection.SentMessages, Has.Count.EqualTo(8));
        Assert.That(host.Services.GetRequiredService<Probe>().Events, Is.Empty);
        Assert.Throws<ArgumentNullException>(() => scope.Initialize(null!));
        await scope.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => scope.Initialize(handler));
    }

    [Test]
    public async Task RegisteredHandlerBroadcastsAndCancellationUseSameContext() {
        await using var host = CreateHost();
        var connection = host.CreateConnection(new TestSession("user", []));
        await host.InvokeAsync(connection, "testing:broadcast", requestId: "broadcast");
        Assert.That(host.Broadcasts.Single().TargetId, Is.EqualTo(connection.Id));
        Assert.That(connection.SentMessages, Has.Count.EqualTo(2));
        using var cancellation = new CancellationTokenSource();
        var probe = host.Services.GetRequiredService<Probe>();
        var pending = host.InvokeAsync(connection, "testing:wait", cancellationToken: cancellation.Token);
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await pending.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(connection.SentMessages, Has.Count.EqualTo(2));
        Assert.ThrowsAsync<OperationCanceledException>(() => host.InvokeAsync(connection, "testing:secure", cancellationToken: cancellation.Token));
    }

    [Test]
    public async Task TestConnectionCapturesCopiesAndImplementsTransportlessLifecycle() {
        using var connection = new DarkWsTestConnection();
        byte[] buffer = [1, 2];
        await connection.SendAsync(buffer);
        buffer[0] = 3;
        Assert.That(connection.SentMessages.Single(), Is.EqualTo(new byte[] { 1, 2 }));
        Assert.Throws<ArgumentNullException>(() => connection.SendAsync(null!));
        Assert.Throws<NotSupportedException>(() => _ = connection.WebSocket);
        Assert.Throws<NotSupportedException>(() => connection.ReceiveMessageAsync());
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => connection.SendAsync([], cancelled.Token));
        Assert.Throws<OperationCanceledException>(() => connection.CloseAsync(cancelled.Token));
        await connection.CloseAsync();
        await connection.SendAsync([]);
        Assert.That(connection.IsOpen, Is.False);
        Assert.That(connection.SentMessages, Has.Count.EqualTo(1));
        connection.Abort();
    }

    [Test]
    public async Task RegistrationOwnershipAndLifetimeAreChecked() {
        Assert.Throws<ArgumentNullException>(() => new DarkWsTestHost(null!));
        Assert.Throws<InvalidOperationException>(() => new DarkWsTestHost(builder =>
            builder.AddHandlersFromAssemblyContaining<EchoHandler>().AddHandlersFromAssemblyContaining<EchoHandler>()));
        var host = CreateHost();
        var connection = host.CreateConnection();
        using var foreign = new DarkWsTestConnection();
        Assert.Throws<ArgumentException>(() => host.CreateScope(foreign));
        Assert.Throws<ArgumentNullException>(() => host.CreateScope(null!));
        Assert.ThrowsAsync<ArgumentException>(() => host.InvokeAsync(connection, " "));
        connection.Dispose();
        Assert.Throws<InvalidOperationException>(() => host.CreateScope(connection));
        await host.DisposeAsync();
        await host.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => host.CreateConnection());
        Assert.Throws<ObjectDisposedException>(() => host.CreateScope(connection));
    }

    public sealed record Payload(string Value);

    public sealed class Probe {
        public List<string> Events { get; } = [];
        public List<Guid> Scopes { get; } = [];
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed class ScopedDependency(Probe probe) : IAsyncDisposable {
        public Guid Id { get; } = Guid.NewGuid();
        private bool _disposed;
        public IEnumerable<string> Deferred(string value) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            probe.Events.Add("serialize");
            yield return value;
        }
        public ValueTask DisposeAsync() {
            _disposed = true;
            probe.Events.Add("dispose");
            return ValueTask.CompletedTask;
        }
    }

    public sealed class Initializer(Probe probe) : IDarkWsScopeInitializer {
        public ValueTask InitializeAsync(IServiceProvider scopedServices, IDarkWsContextAccessor context, CancellationToken cancellationToken) {
            probe.Events.Add("initialize:" + context.Action?.Name);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class Filter(Probe probe) : IDarkWsActionFilter {
        public ValueTask<IResponse> InvokeAsync(DarkWsActionContext context, Func<ValueTask<IResponse>> next) {
            probe.Events.Add("filter:" + (context.Payload as Payload)?.Value);
            return context.Action.Name == "testing:blocked"
                ? ValueTask.FromResult<IResponse>(new ErrorResponse("blocked")) : next();
        }
    }

    [Handler("testing")]
    public sealed class EchoHandler(Probe probe, ScopedDependency dependency) : HandlerBase<TestSession> {
        [Action("echo"), AllowAnonymous]
        public IResponse Echo(Payload payload) {
            probe.Events.Add("handler");
            probe.Scopes.Add(dependency.Id);
            return Ok(dependency.Deferred(payload.Value));
        }
        [Action("secure")]
        public IResponse Secure() => Ok(Session.Id);
        [Action("controlled"), AllowAnonymous]
        public IResponse Controlled() => throw new ErrorResponseException("domain:error");
        [Action("throws"), AllowAnonymous]
        public IResponse Throws() => throw new InvalidOperationException("private details");
        [Action("blocked"), AllowAnonymous]
        public IResponse Blocked() => throw new AssertionException("The filter must short-circuit.");
        [Action("broadcast")]
        public async Task<IResponse> Broadcast() {
            await BroadcastToSelfAsync("changed", Session.Id);
            return Ok();
        }
        [Action("wait")]
        public async Task<IResponse> Wait() {
            probe.Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ConnectionAborted);
            return Ok();
        }
    }

    public sealed class DirectHandler : HandlerBase {
        public (IDarkWsSession, HttpContext, IWebSocketConnection, CancellationToken, IServiceProvider) Current =>
            (Session, HttpContext, Connection, ConnectionAborted, Services);
        public ISession? OptionalAspNetSession => AspNetSession;
        public async Task Broadcasts() {
            await BroadcastAsync("all");
            await BroadcastAsync<string?>("all", null);
            await BroadcastToSelfAsync("self");
            await BroadcastToSelfAsync("self", 1);
            await BroadcastToSessionAsync(Session.Id, "session");
            await BroadcastToSessionAsync(Session.Id, "session", 2);
            await BroadcastToGroupAsync("group", "group");
            await BroadcastToGroupAsync("group", "group", 3);
        }
        public Task MissingRecipient() => BroadcastToSessionAsync("missing", "missing");
    }

    public sealed class TestSession(string id, string[] groups, bool authenticated = true) : IDarkWsSession {
        public string Id => id;
        public IReadOnlyCollection<string> Groups => groups;
        public ClaimsPrincipal User { get; } = new(new ClaimsIdentity([], authenticated ? "test" : null));
    }
}
