using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using DarkWS.Abstractions;
using DarkWS.Test.Project;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        socket.EnqueueClose();
        await accept.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static ServiceProvider CreateProvider(Action<DarkWsOptions>? configure = null) {
        var services = new ServiceCollection();
        var probe = new Probe();
        services.AddSingleton(probe);
        services.AddLogging(builder => builder.AddProvider(probe));
        services.AddDarkWs(options => { options.ShutdownTimeout = TimeSpan.FromMilliseconds(100); configure?.Invoke(options); });
        services.AddScoped<AuditHandler>();
        services.AddScoped<ScopeLifetime>();
        services.AddScoped<IDarkWsAuthenticator, AuditAuthenticator>();
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
    public sealed class ScopeLifetime(Probe probe) : IDisposable {
        public void Dispose() => probe.ScopeDisposed = true;
    }
    [Handler("audit"), AllowAnonymous]
    public sealed class AuditHandler(Probe probe, ScopeLifetime lifetime) : HandlerBase {
        [Action("input")] public IResponse Input(Payload input) { _ = lifetime; probe.Calls++; return Ok(input.Text); }
        [Action("number")] public IResponse Number(int input) { probe.Calls++; return Ok(input); }
        [Action("nullable")] public IResponse Nullable(string? input) { probe.Calls++; return Ok(input); }
        [Action("nullable-number")] public IResponse NullableNumber(int? input) { probe.Calls++; return Ok(input); }
        [Action("json-error")] public IResponse JsonError() => throw new JsonException("handler failure");
        [Action("stubborn")] public async Task<IResponse> Stubborn() { probe.Started.TrySetResult(); await probe.Release.Task; return Ok(); }
    }
    public sealed record Payload(string Text);
    public sealed class AuditAuthenticator : IDarkWsAuthenticator {
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
