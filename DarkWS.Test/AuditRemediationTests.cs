using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using DarkWS.Abstractions;
using DarkWS.Test.Project;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace DarkWS.Test;

public sealed class AuditRemediationTests {
    [TestCase("@")]
    [TestCase("@auth")]
    public async Task ReservedIdsAreRejectedWithoutBroadcastingOrInvokingAnAction(string id) {
        using var provider = CreateProvider();
        var socket = new TestWebSocket();
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(CreateConnection(provider, socket));
        socket.EnqueueReceive(JsonSerializer.Serialize(new { id, action = "audit:nullable" }));
        await UntilAsync(() => socket.Sent.Count == 1);
        var response = JsonSerializer.Deserialize<ErrorMessage>(socket.Sent.Single(), Utils.JsonOptions)!;
        Assert.That(response.Id, Is.Empty);
        Assert.That(response.Error, Is.EqualTo("darkws:error:invalid-request"));
        Assert.That(provider.GetRequiredService<Probe>().Calls, Is.Zero);
        socket.EnqueueReceive("{");
        socket.EnqueueReceive("{\"id\":\"valid\",\"action\":\"audit:nullable\"}");
        await UntilAsync(() => socket.Sent.Count == 2);
        Assert.That(provider.GetRequiredService<Probe>().Calls, Is.EqualTo(1));
        Assert.That(provider.GetRequiredService<Probe>().Logs, Is.Empty);
        socket.EnqueueClose();
        await accept.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public void ContextOutsideMessageScopeHasExplicitDiagnosticForEveryProperty() {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IDarkWsContextAccessor>();
        foreach (var property in typeof(IDarkWsContextAccessor).GetProperties()) {
            var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => property.GetValue(context));
            Assert.That(error!.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(error.InnerException!.Message, Does.Contain("message scope"));
        }
    }

    [Test]
    public void StandardOptionsPipelineIncludesConfigureAndPostConfigure() {
        var services = new ServiceCollection();
        services.Configure<DarkWsOptions>(options => options.MaxMessageSizeBytes = 1024);
        services.AddDarkWs(options => options.MaxMessageSizeBytes = 65536);
        services.Configure<DarkWsOptions>(options => options.MaxMessageSizeBytes = 777);
        services.PostConfigure<DarkWsOptions>(options => options.SendTimeout = TimeSpan.FromSeconds(7));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var options = provider.GetRequiredService<IOptions<DarkWsOptions>>().Value;
        Assert.That(options.MaxMessageSizeBytes, Is.EqualTo(777));
        Assert.That(options.SendTimeout, Is.EqualTo(TimeSpan.FromSeconds(7)));
        Assert.That(provider.GetRequiredService<IOptionsMonitor<DarkWsOptions>>().CurrentValue.MaxMessageSizeBytes, Is.EqualTo(777));
        Assert.That(scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<DarkWsOptions>>().Value.MaxMessageSizeBytes, Is.EqualTo(777));
    }

    [Test]
    public void InvalidFinalConfigurationFailsOnHostStartup() {
        using var host = new HostBuilder().ConfigureServices(services => {
            services.AddDarkWs();
            services.PostConfigure<DarkWsOptions>(options => options.ShutdownTimeout = TimeSpan.Zero);
        }).Build();
        Assert.ThrowsAsync<OptionsValidationException>(async () => await host.StartAsync());
    }

    [TestCase("input", "", true)]
    [TestCase("input", ",\"data\":null", true)]
    [TestCase("input", ",\"data\":{\"text\":[1,2]}", true)]
    [TestCase("number", ",\"data\":99999999999", true)]
    [TestCase("number", "", true)]
    [TestCase("nullable", "", false)]
    [TestCase("nullable", ",\"data\":null", false)]
    [TestCase("nullable-number", "", false)]
    [TestCase("input", ",\"data\":{\"text\":\"ok\"}", false)]
    public async Task PayloadContractDistinguishesInvalidAndNullableInput(string action, string payload, bool invalid) {
        using var provider = CreateProvider();
        var socket = new TestWebSocket();
        var connection = CreateConnection(provider, socket);
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(connection);
        socket.EnqueueReceive($$"""{"id":"payload","action":"audit:{{action}}"{{payload}}}""");
        await UntilAsync(() => socket.Sent.Count == 1);
        using var response = JsonDocument.Parse(socket.Sent.Single());
        Assert.That(response.RootElement.TryGetProperty("error", out var error), Is.EqualTo(invalid));
        if (invalid) Assert.That(error.GetString(), Is.EqualTo("darkws:error:invalid-request"));
        Assert.That(provider.GetRequiredService<Probe>().Calls, Is.EqualTo(invalid ? 0 : 1));
        socket.EnqueueClose();
        await accept.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task HandlerJsonExceptionRemainsServerFailureAndSendFailureIsObserved() {
        using var provider = CreateProvider();
        var socket = new TestWebSocket();
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(CreateConnection(provider, socket));
        socket.EnqueueReceive("{\"id\":\"server\",\"action\":\"audit:json-error\"}");
        await UntilAsync(() => socket.Sent.Count == 1);
        Assert.That(JsonSerializer.Deserialize<ErrorMessage>(socket.Sent.Single(), Utils.JsonOptions)!.Error, Is.EqualTo("darkws:error:request-failed"));
        socket.SendError = new InvalidOperationException("test write failure");
        socket.EnqueueReceive("{\"id\":\"write\",\"action\":\"audit:nullable\"}");
        await UntilAsync(() => provider.GetRequiredService<Probe>().Logs.Any(log => log.Contains("Cannot process or send response")));
        socket.EnqueueClose();
        await accept.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(socket.WasDisposed, Is.True);
    }

    [TestCase("cycle", "Cannot process or send response for broken")]
    [TestCase("throwing-response", "Cannot process or send response for broken")]
    [TestCase("null", "audit:null failed in")]
    public async Task ResultFailureAnswersWithRequestFailedAndKeepsTheConnection(string action, string log) {
        using var provider = CreateProvider();
        var socket = new TestWebSocket();
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(CreateConnection(provider, socket));
        socket.EnqueueReceive($$"""{"id":"broken","action":"audit:{{action}}"}""");
        await UntilAsync(() => socket.Sent.Count == 1);
        Assert.That(JsonSerializer.Deserialize<ErrorMessage>(socket.Sent.Single(), Utils.JsonOptions), Is.EqualTo(new ErrorMessage("broken", "darkws:error:request-failed")));
        Assert.That(provider.GetRequiredService<Probe>().Logs, Has.Some.Contains(log));
        socket.EnqueueReceive("{\"id\":\"next\",\"action\":\"audit:nullable\"}");
        await UntilAsync(() => socket.Sent.Count == 2);
        socket.EnqueueClose();
        await accept.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task DeferredResultIsSerializedBeforeTheMessageScopeEnds() {
        using var provider = CreateProvider();
        var socket = new TestWebSocket();
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(CreateConnection(provider, socket));
        socket.EnqueueReceive("{\"id\":\"deferred\",\"action\":\"audit:deferred\"}");
        await UntilAsync(() => socket.Sent.Count == 1);
        Assert.That(JsonSerializer.Deserialize<ResponseMessage<int[]>>(socket.Sent.Single(), Utils.JsonOptions)!.Data, Is.EqualTo(new[] { 1, 2 }));
        await UntilAsync(() => provider.GetRequiredService<Probe>().ScopeDisposed);
        socket.EnqueueClose();
        await accept.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task FailureAfterAResponseWasSentIsOnlyLogged() {
        using var provider = CreateProvider();
        var socket = new TestWebSocket();
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(CreateConnection(provider, socket));
        socket.EnqueueReceive("{\"id\":\"partial\",\"action\":\"audit:partial\"}");
        await UntilAsync(() => provider.GetRequiredService<Probe>().Logs.Any(log => log.Contains("Cannot process or send response for partial")));
        socket.EnqueueReceive("{\"id\":\"next\",\"action\":\"audit:nullable\"}");
        await UntilAsync(() => socket.Sent.Count == 2);
        Assert.That(socket.Sent.Select(System.Text.Encoding.UTF8.GetString), Is.EqualTo(new[] { "{\"id\":\"partial\",\"data\":1}", "{\"id\":\"next\",\"data\":null}" }));
        socket.EnqueueClose();
        await accept.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task CloseHooksObserveTheShutdownDeadline() {
        var hook = new CloseHook();
        using var provider = CreateProvider(register: services => services.AddSingleton<DarkWsMiddleware>(hook));
        var socket = new TestWebSocket();
        socket.EnqueueClose();
        await provider.GetRequiredService<WebSocketHandler>().AcceptAsync(CreateConnection(provider, socket)).WaitAsync(TimeSpan.FromSeconds(2));
        // The hook waits for its token, so completing at all proves the deadline fires after entry.
        Assert.That(await hook.CancelledOnEntry.Task.WaitAsync(TimeSpan.FromSeconds(2)), Is.False);
    }

    [Test]
    public async Task RefreshAfterShutdownDoesNotResurrectTheConnection() {
        using var provider = CreateProvider();
        var socket = new TestWebSocket();
        var connection = CreateConnection(provider, socket);
        var storage = provider.GetRequiredService<ConnectionStorage>();
        socket.EnqueueClose();
        await provider.GetRequiredService<WebSocketHandler>().AcceptAsync(connection).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(storage.Add(connection), Is.SameAs(connection));
        Assert.That(storage.GetAll(), Is.Empty);
    }

    [Test]
    public async Task ShutdownIsBoundedAndDefersDisposalUntilUncooperativeHandlerCompletes() {
        using var provider = CreateProvider();
        var socket = new TestWebSocket();
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(CreateConnection(provider, socket));
        var probe = provider.GetRequiredService<Probe>();
        socket.EnqueueReceive("{\"id\":\"slow\",\"action\":\"audit:stubborn\"}");
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        socket.EnqueueClose();
        try {
            await accept.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.That(provider.GetRequiredService<ConnectionStorage>().GetAll(), Is.Empty);
            Assert.That(socket.WasAborted, Is.True);
            Assert.That(socket.WasDisposed, Is.False);
            Assert.That(probe.ScopeDisposed, Is.False);
        } finally {
            probe.Release.TrySetResult();
            await UntilAsync(() => socket.WasDisposed);
        }
        Assert.That(probe.ScopeDisposed, Is.True);
        Assert.That(socket.Sent, Is.Empty);
    }

    [Test]
    public async Task ShutdownCloseHandshakeHasSameBoundedDeadline() {
        using var provider = CreateProvider();
        var socket = new TestWebSocket { CloseBarrier = new TaskCompletionSource().Task };
        socket.EnqueueClose();
        await provider.GetRequiredService<WebSocketHandler>().AcceptAsync(CreateConnection(provider, socket)).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(socket.WasAborted, Is.True);
        await UntilAsync(() => socket.WasDisposed);
        Assert.That(socket.WasDisposed, Is.True);
        Assert.That(provider.GetRequiredService<ConnectionStorage>().GetAll(), Is.Empty);
    }

    [Test]
    public void SendTimeoutAbortsSocket() {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var socket = new TestWebSocket { SendBarrier = gate.Task };
        using var connection = new WebSocketConnection(socket, new DefaultHttpContext(), null) { SendTimeout = TimeSpan.FromMilliseconds(50) };
        Assert.ThrowsAsync<TimeoutException>(async () => await connection.SendAsync([1]));
        Assert.That(socket.WasAborted, Is.True);
    }

    [Test]
    public void OversizeCloseOutputIsBounded() {
        var socket = new TestWebSocket { CloseBarrier = new TaskCompletionSource().Task };
        socket.EnqueueReceive("too large");
        using var connection = new WebSocketConnection(socket, new DefaultHttpContext(), null, 1) { SendTimeout = TimeSpan.FromMilliseconds(50) };
        Assert.ThrowsAsync<TimeoutException>(async () => await connection.ReceiveMessageAsync());
        Assert.That(socket.WasAborted, Is.True);
    }

    [Test]
    public async Task DisposeDefersResourcesUntilExistingWriterAndWaitersFinish() {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var socket = new TestWebSocket { SendBarrier = gate.Task, IgnoreSendCancellation = true };
        var connection = new WebSocketConnection(socket, new DefaultHttpContext(), null);
        var first = connection.SendAsync([1]);
        await socket.SendStarted.Task;
        var second = connection.SendAsync([2]);
        connection.Dispose();
        Assert.That(socket.WasDisposed, Is.False);
        gate.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(socket.WasDisposed, Is.True);
        await connection.SendAsync([3]);
        connection.Dispose();
        Assert.That(socket.Sent, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task BroadcastSerializesOnceAndSharesBytesAcrossRecipients() {
        var converter = new BroadcastConverter();
        using var provider = CreateProvider(options => options.JsonOptions.Converters.Add(converter));
        var sockets = Enumerable.Range(0, 20).Select(_ => new TestWebSocket()).ToArray();
        var storage = provider.GetRequiredService<ConnectionStorage>();
        foreach (var socket in sockets) storage.Add(CreateConnection(provider, socket));
        await provider.GetRequiredService<Broadcaster>().DeliverAsync(new DarkWsBroadcast(DarkWsTarget.All, null, "one", null), default);
        Assert.That(converter.Writes, Is.EqualTo(1));
        var bytes = sockets[0].SentBuffers.Single();
        Assert.That(sockets.All(socket => ReferenceEquals(socket.SentBuffers.Single(), bytes)), Is.True);
        foreach (var connection in storage.GetAll()) connection.Dispose();
    }

    [Test]
    public async Task PublisherCancellationDoesNotCancelDeliveryToOtherConnections() {
        using var provider = CreateProvider();
        foreach (var service in provider.GetServices<IHostedService>()) await service.StartAsync(default);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sockets = Enumerable.Range(0, 3).Select(_ => new TestWebSocket { SendBarrier = gate.Task }).ToArray();
        var storage = provider.GetRequiredService<ConnectionStorage>();
        foreach (var socket in sockets) storage.Add(CreateConnection(provider, socket));
        using var publisher = new CancellationTokenSource();
        var broadcast = provider.GetRequiredService<IBroadcaster>().BroadcastAsync("changed", publisher.Token);
        await Task.WhenAll(sockets.Select(socket => socket.SendStarted.Task)).WaitAsync(TimeSpan.FromSeconds(2));
        publisher.Cancel();
        gate.SetResult();
        await broadcast.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(sockets.Select(socket => socket.Sent.Count), Is.All.EqualTo(1));
        Assert.That(sockets.Select(socket => socket.WasAborted), Is.All.False);
        foreach (var connection in storage.GetAll()) connection.Dispose();
    }

    [Test]
    public async Task BroadcastStartsEveryRecipientWithoutWaitingForSlowOnes() {
        using var provider = CreateProvider();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sockets = Enumerable.Range(0, Environment.ProcessorCount + 1).Select(_ => new TestWebSocket { SendBarrier = gate.Task }).ToArray();
        var storage = provider.GetRequiredService<ConnectionStorage>();
        foreach (var socket in sockets) storage.Add(CreateConnection(provider, socket));
        var delivery = provider.GetRequiredService<Broadcaster>().DeliverAsync(new DarkWsBroadcast(DarkWsTarget.All, null, "changed", null), default).AsTask();
        // More recipients than processors all start at once, so held sockets do not delay the rest.
        await Task.WhenAll(sockets.Select(socket => socket.SendStarted.Task)).WaitAsync(TimeSpan.FromSeconds(2));
        gate.SetResult();
        await delivery.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(sockets.Select(socket => socket.Sent.Count), Is.All.EqualTo(1));
        foreach (var connection in storage.GetAll()) connection.Dispose();
    }

    [Test]
    public async Task ActionKeepsItsSessionWhenLogoutArrivesMeanwhile() {
        using var provider = CreateProvider();
        var probe = provider.GetRequiredService<Probe>();
        var socket = new TestWebSocket();
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(CreateConnection(provider, socket));
        socket.EnqueueReceive("auth:first");
        await UntilAsync(() => socket.Sent.Count == 1);
        socket.EnqueueReceive("{\"id\":\"who\",\"action\":\"audit:session-after-release\"}");
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        socket.EnqueueReceive("logout");
        await UntilAsync(() => socket.Sent.Count == 2);
        probe.Release.TrySetResult();
        await UntilAsync(() => socket.Sent.Count == 3);
        Assert.That(JsonSerializer.Deserialize<ResponseMessage<string>>(socket.Sent.Last(), Utils.JsonOptions), Is.EqualTo(new ResponseMessage<string>("who", "first")));
        socket.EnqueueClose();
        await accept.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task UpgradeUserFollowsTheAuthenticatorDecision() {
        using var host = await new HostBuilder().ConfigureWebHost(builder => builder
            .UseTestServer()
            .ConfigureServices(services => {
                services.AddRouting();
                services.AddSingleton(new Probe());
                services.AddScoped<ScopeLifetime>();
                services.AddScoped<AuditHandler>();
                services.AddDarkWs();
                services.AddScoped<IDarkWsAuthenticator, RejectingAuthenticator>();
            })
            .Configure(app => {
                app.UseRouting();
                // Stands in for cookie authentication whose user the DarkWS authenticator then rejects.
                app.Use((context, next) => {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "cookie")], "Cookie"));
                    return next(context);
                });
                app.UseWebSockets();
                app.UseEndpoints(endpoints => endpoints.MapDarkWs());
            })).StartAsync();
        host.Services.GetRequiredService<DarkWsActionRegistry>().Add(typeof(AuditHandler));
        using var socket = await host.GetTestServer().CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/ws"), CancellationToken.None);
        await socket.SendTextAsync("{\"id\":\"user\",\"action\":\"audit:user-authenticated\"}");
        Assert.That(await socket.ReceiveMessage<ResponseMessage<bool>>(), Is.EqualTo(new ResponseMessage<bool>("user", false)));
        await host.StopAsync();
    }

    [Test]
    public async Task TransportlessConnectionTimeoutDoesNotBreakTheBroadcast() {
        using var provider = CreateProvider(options => options.BroadcastSendTimeout = TimeSpan.FromMilliseconds(50));
        var storage = provider.GetRequiredService<ConnectionStorage>();
        var socket = new TestWebSocket();
        storage.Add(new TransportlessConnection());
        storage.Add(CreateConnection(provider, socket));
        await provider.GetRequiredService<Broadcaster>().DeliverAsync(new DarkWsBroadcast(DarkWsTarget.All, null, "changed", null), default).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(socket.Sent, Has.Count.EqualTo(1));
        Assert.That(provider.GetRequiredService<Probe>().Logs, Has.Some.Contains("Cannot abort"));
        foreach (var connection in storage.GetAll()) connection.Dispose();
    }

    [Test]
    public async Task DefaultAuthenticatorKeepsOneFallbackIdPerConnection() {
        var authenticator = new AspNetDarkWsAuthenticator();
        var user = new ClaimsPrincipal(new ClaimsIdentity([], "Cookie"));
        var context = new DefaultHttpContext { User = user };
        var first = await authenticator.AuthenticateAsync(context, "any", default);
        var second = await authenticator.AuthenticateAsync(context, "other", default);
        Assert.That(second!.Id, Is.EqualTo(first!.Id));
        Assert.That((await authenticator.AuthenticateAsync(new DefaultHttpContext { User = user }, null, default))!.Id, Is.Not.EqualTo(first.Id));
    }

    [Test]
    public async Task DataFieldSurvivesWhenWritingNullForResponsesAndBroadcasts() {
        using var provider = CreateProvider(options => options.JsonOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
        foreach (var service in provider.GetServices<IHostedService>()) await service.StartAsync(default);
        var socket = new TestWebSocket();
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(CreateConnection(provider, socket));
        socket.EnqueueReceive("{\"id\":\"empty\",\"action\":\"audit:nullable\"}");
        await UntilAsync(() => socket.Sent.Count == 1);
        await provider.GetRequiredService<IBroadcaster>().BroadcastAsync<object?>("changed", null);
        await UntilAsync(() => socket.Sent.Count == 2);
        Assert.That(socket.Sent.Select(System.Text.Encoding.UTF8.GetString), Is.EqualTo(new[] { "{\"id\":\"empty\",\"data\":null}", "{\"id\":\"@\",\"action\":\"changed\",\"data\":null}" }));
        socket.EnqueueClose();
        await accept.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public void BroadcastEnvelopeKeepsExplicitNullApartFromMissingData() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var none = JsonSerializer.Serialize(new DarkWsBroadcast(DarkWsTarget.All, null, "a", null), options);
        var explicitNull = JsonSerializer.Serialize(new DarkWsBroadcast(DarkWsTarget.All, null, "a", JsonSerializer.SerializeToElement<object?>(null)), options);
        Assert.That(none, Does.Not.Contain("data"));
        Assert.That(JsonSerializer.Deserialize<DarkWsBroadcast>(none, options)!.Data, Is.Null);
        Assert.That(JsonSerializer.Deserialize<DarkWsBroadcast>(explicitNull, options)!.Data?.ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task AuthenticationFailureAndLogoutClearSessionAndIndexesWithoutClosing() {
        using var provider = CreateProvider();
        var socket = new TestWebSocket();
        var connection = CreateConnection(provider, socket);
        var storage = provider.GetRequiredService<ConnectionStorage>();
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(connection);
        foreach (var token in new[] { "first", "rejected", "second", "", "third", "throws", "last" }) {
            socket.EnqueueReceive("auth:" + token);
            await UntilAsync(() => socket.Sent.Count == 1);
            socket.Sent.TryDequeue(out var bytes);
            var valid = token is "first" or "second" or "third" or "last";
            Assert.That(System.Text.Encoding.UTF8.GetString(bytes!), Is.EqualTo(valid ? "auth:success" : "auth:failed"));
            Assert.That(connection.Session?.Id, Is.EqualTo(valid ? token : null));
            Assert.That(connection.HttpContext.User.Identity?.IsAuthenticated, Is.EqualTo(valid));
            Assert.That(storage.GetByGroup("session:first"), valid && token == "first" ? Has.Count.EqualTo(1) : Is.Empty);
        }
        socket.EnqueueReceive("logout");
        await UntilAsync(() => socket.Sent.Count == 1);
        Assert.That(System.Text.Encoding.UTF8.GetString(socket.Sent.Single()), Is.EqualTo("logout:success"));
        Assert.That(connection.Session, Is.Null);
        Assert.That(storage.GetBySession("last"), Is.Empty);
        Assert.That(connection.IsOpen, Is.True);
        // Every non-empty auth: token resolves the authenticator from its own scope.
        Assert.That(provider.GetRequiredService<Probe>().Authenticators, Is.EqualTo(6));
        socket.EnqueueClose();
        await accept.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static ServiceProvider CreateProvider(Action<DarkWsOptions>? configure = null, Action<IServiceCollection>? register = null) {
        var services = new ServiceCollection();
        var probe = new Probe();
        services.AddSingleton(probe);
        services.AddLogging(builder => builder.AddProvider(probe));
        services.AddDarkWs(options => { options.ShutdownTimeout = TimeSpan.FromMilliseconds(100); configure?.Invoke(options); });
        services.AddScoped<AuditHandler>();
        services.AddScoped<ScopeLifetime>();
        services.AddScoped<IDarkWsAuthenticator, AuditAuthenticator>();
        register?.Invoke(services);
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<DarkWsActionRegistry>().Add(typeof(AuditHandler));
        return provider;
    }

    private static WebSocketConnection CreateConnection(ServiceProvider provider, TestWebSocket socket) => new(socket, new DefaultHttpContext { RequestServices = provider }, null);
    private static async Task UntilAsync(Func<bool> condition) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }

    public sealed class Probe : ILoggerProvider, ILogger {
        public int Calls;
        public int Authenticators;
        public bool ScopeDisposed;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public System.Collections.Concurrent.ConcurrentQueue<string> Logs { get; } = new();
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Logs.Enqueue(formatter(state, exception));
    }
    public sealed class CloseHook : DarkWsMiddleware {
        public TaskCompletionSource<bool> CancelledOnEntry { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async Task OnCloseAsync(IDarkWsContextAccessor context) {
            var cancelled = context.ConnectionAborted.IsCancellationRequested;
            try { await Task.Delay(Timeout.Infinite, context.ConnectionAborted); } catch (OperationCanceledException) { }
            CancelledOnEntry.TrySetResult(cancelled);
        }
    }
    public sealed class ScopeLifetime(Probe probe) : IDisposable {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = probe.ScopeDisposed = true;
    }
    [Handler("audit"), AllowAnonymous]
    public sealed class AuditHandler(Probe probe, ScopeLifetime lifetime) : HandlerBase {
        [Action("input")] public IResponse Input(Payload input) { _ = lifetime; probe.Calls++; return Ok(input.Text); }
        [Action("number")] public IResponse Number(int input) { probe.Calls++; return Ok(input); }
        [Action("nullable")] public IResponse Nullable(string? input) { probe.Calls++; return Ok(input); }
        [Action("nullable-number")] public IResponse NullableNumber(int? input) { probe.Calls++; return Ok(input); }
        [Action("json-error")] public IResponse JsonError() => throw new JsonException("handler failure");
        [Action("stubborn")] public async Task<IResponse> Stubborn() { probe.Started.TrySetResult(); await probe.Release.Task; return Ok(); }
        [Action("cycle")] public IResponse Cycle() { var node = new Node(); node.Next = node; return Ok(node); }
        [Action("deferred")] public IResponse Deferred() => Ok(Enumerable.Range(1, 2).Select(value => lifetime.Disposed ? throw new ObjectDisposedException(nameof(ScopeLifetime)) : value));
        [Action("null")] public IResponse Null() => null!;
        [Action("throwing-response")] public IResponse ThrowingResponse() => new FailingResponse(sendFirst: false);
        [Action("partial")] public IResponse Partial() => new FailingResponse(sendFirst: true);
        [Action("session-after-release")] public async Task<IResponse> SessionAfterRelease() { probe.Started.TrySetResult(); await probe.Release.Task; return Ok(Session.Id); }
        [Action("user-authenticated")] public IResponse UserAuthenticated() => Ok(HttpContext.User.Identity?.IsAuthenticated == true);
    }
    public sealed class RejectingAuthenticator : IDarkWsAuthenticator {
        public ValueTask<IDarkWsSession?> AuthenticateAsync(HttpContext context, string? token, CancellationToken cancellationToken) => ValueTask.FromResult<IDarkWsSession?>(null);
    }
    private sealed class TransportlessConnection : IWebSocketConnection {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public WebSocket WebSocket => throw new NotSupportedException("This connection has no transport");
        public HttpContext HttpContext { get; } = new DefaultHttpContext();
        public IDarkWsSession? Session => null;
        public bool IsOpen => true;
        public Task<ReceivedMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendAsync(byte[] data, CancellationToken cancellationToken = default) => Task.Delay(Timeout.Infinite, cancellationToken);
        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }
    public sealed class Node { public Node? Next { get; set; } }
    private sealed class FailingResponse(bool sendFirst) : IResponse {
        public async Task WriteResultAsync(ResponseContext context, CancellationToken cancellationToken = default) {
            if (sendFirst) await context.SendAsync(new { id = context.RequestId, data = 1 }, cancellationToken);
            throw new InvalidOperationException("response failure");
        }
    }
    public sealed record Payload(string Text);
    public sealed class AuditAuthenticator : IDarkWsAuthenticator {
        public AuditAuthenticator(Probe probe) => Interlocked.Increment(ref probe.Authenticators);
        public ValueTask<IDarkWsSession?> AuthenticateAsync(HttpContext context, string? token, CancellationToken cancellationToken) {
            if (token == "throws") throw new InvalidOperationException("test authentication failure");
            return ValueTask.FromResult<IDarkWsSession?>(string.IsNullOrEmpty(token) || token == "rejected" ? null : new TestSession(token, new ClaimsPrincipal(new ClaimsIdentity([], "Test"))));
        }
    }
    private sealed class BroadcastConverter : JsonConverter<BroadcastActionMessage> {
        public int Writes;
        public override BroadcastActionMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer, BroadcastActionMessage value, JsonSerializerOptions options) {
            Writes++;
            writer.WriteStartObject(); writer.WriteString("id", value.Id); writer.WriteString("action", value.Action); writer.WriteEndObject();
        }
    }
}
