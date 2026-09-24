using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DarkWS.Client.Test;

[TestFixture]
public sealed class ClientTests {
    private static TaskCompletionSource<T> Completion<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static SemaphoreSlim SendGate(DarkWsClient client) {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var cycle = typeof(DarkWsClient).GetField("_cycle", flags)!.GetValue(client)!;
        var connection = cycle.GetType().GetField("Connection", flags)!.GetValue(cycle)!;
        return (SemaphoreSlim)connection.GetType().GetField("SendGate", flags)!.GetValue(connection)!;
    }
    private static DarkWsClient Client(TestServer server, Action<DarkWsClientOptions>? configure = null) {
        var options = new DarkWsClientOptions { Endpoint = server.Endpoint, Reconnect = false, CloseTimeout = TimeSpan.FromMilliseconds(100) };
        configure?.Invoke(options);
        return new DarkWsClient(options);
    }

    [Test]
    public async Task RealServerSupportsDirectAndDiClients() {
        await using var server = await TestServer.StartAsync(darkWs: true);
        await using var client = new DarkWsClient(server.Endpoint);
        Assert.That(client.State, Is.EqualTo(DarkWsClientState.Disconnected));
        Assert.That(await client.RequestAsync<int>("echo:value", 42), Is.EqualTo(42));
        await client.RequestAsync("echo:empty");
        await client.RequestAsync("echo:value", new { value = 1 });
        Assert.That(await client.RequestAsync<string?>("echo:value", (object?)null), Is.Null);
        var error = Assert.ThrowsAsync<DarkWsResponseException>(() => client.RequestAsync("echo:error"))!;
        Assert.Multiple(() => {
            Assert.That(error.Code, Is.EqualTo("test:rejected"));
            Assert.That(error.ErrorData!.Value.GetInt32(), Is.EqualTo(7));
            Assert.That(error.Action, Is.EqualTo("echo:error"));
            Assert.That(error.RequestId, Is.Not.Empty);
        });
        var notification = Completion<int>();
        using var subscription = client.On<Changed>("changed", value => notification.TrySetResult(value.Value));
        await client.RequestAsync("echo:notify");
        Assert.That(await notification.Task.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(11));
        await client.AuthenticateAsync("valid");
        Assert.That(await client.RequestAsync<int>("private:value"), Is.EqualTo(123));
        await client.LogoutAsync();
        Assert.ThrowsAsync<DarkWsResponseException>(() => client.RequestAsync("private:value"));

        var services = new ServiceCollection();
        services.AddSingleton(server.Endpoint);
        services.AddDarkWsClient((provider, options) => options.Endpoint = provider.GetRequiredService<Uri>());
        await using var provider = services.BuildServiceProvider();
        var injected = provider.GetRequiredService<IDarkWsClient>();
        Assert.That(provider.GetRequiredService<IDarkWsClient>(), Is.SameAs(injected));
        Assert.That(injected.State, Is.EqualTo(DarkWsClientState.Disconnected));
        Assert.That(await injected.RequestAsync<int>("echo:value", 8), Is.EqualTo(8));
        await client.CloseAsync();
        Assert.ThrowsAsync<DarkWsConnectionException>(() => client.RequestAsync("echo:empty"));
        await client.ConnectAsync();
        await client.RequestAsync("echo:empty");
    }

    [Test]
    public async Task ResponsesAreCorrelatedAndPayloadPresenceIsPreserved() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server);
        var tasks = Enumerable.Range(0, 20).Select(i => client.RequestAsync<int>("echo", i)).ToArray();
        var socket = await server.AcceptAsync();
        var requests = new List<JsonElement>();
        for (var i = 0; i < tasks.Length; i++) requests.Add(await TestServer.RequestAsync(socket));
        foreach (var request in requests.AsEnumerable().Reverse())
            await TestServer.ReplyAsync(socket, request, "\"data\":" + request.GetProperty("data").GetInt32());
        Assert.That(await Task.WhenAll(tasks), Is.EqualTo(Enumerable.Range(0, 20)));
        Assert.That(server.Connections, Is.EqualTo(1));
        var empty = client.RequestAsync("empty");
        var emptyRequest = await TestServer.RequestAsync(socket);
        Assert.That(emptyRequest.TryGetProperty("data", out _), Is.False);
        Assert.That(emptyRequest.TryGetProperty("payload", out _), Is.False);
        await TestServer.ReplyAsync(socket, emptyRequest, "");
        await empty;
        var explicitNull = client.RequestAsync("null", (object?)null);
        var nullRequest = await TestServer.RequestAsync(socket);
        Assert.That(nullRequest.GetProperty("data").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(nullRequest.TryGetProperty("payload", out _), Is.False);
        await TestServer.ReplyAsync(socket, nullRequest);
        await explicitNull;
    }

    [Test]
    public async Task RequestsUseTheConfiguredEncoder() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server, options => options.JsonOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
        var request = client.RequestAsync("encode", "<a&b>");
        var socket = await server.AcceptAsync();
        var raw = await TestServer.ReadAsync(socket);
        Assert.That(raw, Does.Contain("\"data\":\"<a&b>\""));
        using var document = JsonDocument.Parse(raw);
        await TestServer.ReplyAsync(socket, document.RootElement, "");
        await request;
    }

    [Test]
    public async Task ErrorsMissingDataAndLateResponsesDoNotBreakOtherRequests() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server, options => options.RequestTimeout = TimeSpan.FromMilliseconds(80));
        var timed = client.RequestAsync<int>("timeout");
        var socket = await server.AcceptAsync();
        var timedRequest = await TestServer.RequestAsync(socket);
        Assert.That(Assert.ThrowsAsync<DarkWsTimeoutException>(async () => await timed)!.Stage, Is.EqualTo(DarkWsTimeoutStage.Response));
        await TestServer.ReplyAsync(socket, timedRequest);
        await TestServer.SendAsync(socket, "{\"id\":\"@auth\"}");
        await TestServer.SendAsync(socket, "{\"id\":\"unknown\",\"data\":1}");
        foreach (var fields in new[] { "", "\"data\":\"wrong\"", "\"error\":\"\",\"data\":7" }) {
            var request = client.RequestAsync<int>("test");
            await TestServer.ReplyAsync(socket, await TestServer.RequestAsync(socket), fields);
            if (fields.Length == 0) Assert.ThrowsAsync<DarkWsProtocolException>(async () => await request);
            else if (fields.StartsWith("\"data", StringComparison.Ordinal)) Assert.ThrowsAsync<JsonException>(async () => await request);
            else Assert.That(Assert.ThrowsAsync<DarkWsResponseException>(async () => await request)!.Code, Is.Empty);
        }
        var good = client.RequestAsync<JsonElement>("good");
        await TestServer.ReplyAsync(socket, await TestServer.RequestAsync(socket), "\"data\":{\"value\":42}");
        Assert.That((await good).GetProperty("value").GetInt32(), Is.EqualTo(42));
    }

    [Test]
    public async Task CancellationAndCapacityAreLocalToTheCaller() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server, options => { options.MaxPendingRequests = 1; options.RequestTimeout = Timeout.InfiniteTimeSpan; });
        using var cancellation = new CancellationTokenSource();
        var request = client.RequestAsync<int>("slow", cancellation.Token);
        var socket = await server.AcceptAsync();
        var old = await TestServer.RequestAsync(socket);
        Assert.ThrowsAsync<DarkWsClientLimitException>(() => client.RequestAsync<int>("excess"));
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await request);
        await TestServer.ReplyAsync(socket, old);
        var next = client.RequestAsync<int>("next");
        await TestServer.ReplyAsync(socket, await TestServer.RequestAsync(socket));
        Assert.That(await next, Is.EqualTo(42));
    }

    [Test]
    public async Task ConnectionWaitersShareAttemptsAndCancelIndependently() {
        await using var server = await TestServer.StartAsync();
        var barrier = Completion<bool>();
        await using var client = Client(server, options => options.ConfigureWebSocketOptionsAsync = async (_, token) => await barrier.Task.WaitAsync(token));
        using var cancellation = new CancellationTokenSource();
        var first = client.ConnectAsync(cancellation.Token);
        var second = client.ConnectAsync();
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await first);
        barrier.SetResult(true);
        await second;
        Assert.That(server.Connections, Is.EqualTo(1));
    }

    [Test]
    public async Task ReconnectRejectsOldRequestsAndKeepsSubscriptions() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server, options => options.Reconnect = true);
        var reconnected = Completion<bool>();
        var states = new List<DarkWsClientState>();
        client.StateChanged += (_, change) => { lock (states) states.Add(change.State); if (change.State == DarkWsClientState.Reconnecting) reconnected.TrySetResult(true); };
        var notification = Completion<bool>();
        using var subscription = client.On("changed", () => notification.TrySetResult(true));
        var old = client.RequestAsync("old");
        var first = await server.AcceptAsync();
        await TestServer.RequestAsync(first);
        await first.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "restart", CancellationToken.None);
        Assert.ThrowsAsync<DarkWsConnectionException>(async () => await old);
        await reconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var next = client.RequestAsync<int>("new");
        var second = await server.AcceptAsync();
        var nextRequest = await TestServer.RequestAsync(second);
        Assert.That(nextRequest.GetProperty("action").GetString(), Is.EqualTo("new"));
        await TestServer.ReplyAsync(second, nextRequest);
        Assert.That(await next, Is.EqualTo(42));
        await TestServer.SendAsync(second, "{\"id\":\"@\",\"action\":\"changed\"}");
        await notification.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await client.CloseAsync();
        lock (states) Assert.That(states, Does.Contain(DarkWsClientState.Reconnecting));
    }

    [Test]
    public async Task AsyncSubscribersCanRequestAndFailuresAreIsolated() {
        await using var server = await TestServer.StartAsync(darkWs: true);
        await using var client = Client(server);
        var error = Completion<Exception>();
        var result = Completion<int>();
        client.Error += (_, args) => error.TrySetResult(args.Exception);
        client.Error += (_, _) => throw new InvalidOperationException("observer");
        using var broken = client.On<Changed>("changed", _ => throw new InvalidOperationException("subscriber"));
        using var healthy = client.OnAsync<Changed>("changed", async (value, token) =>
            result.TrySetResult(await client.RequestAsync<int>("echo:value", value.Value, token)));
        await client.RequestAsync("echo:notify");
        Assert.That(await result.Task.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(11));
        Assert.That((await error.Task.WaitAsync(TimeSpan.FromSeconds(5))).Message, Is.EqualTo("subscriber"));
        broken.Dispose();
        broken.Dispose();
    }

    [Test]
    public async Task NotificationOverflowStopsReconnectAndDisposeDoesNotWaitForHandler() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server, options => { options.NotificationQueueCapacity = 1; options.Reconnect = true; });
        var entered = Completion<bool>();
        var release = Completion<bool>();
        var error = Completion<Exception>();
        client.Error += (_, args) => error.TrySetResult(args.Exception);
        using var subscription = client.OnAsync<int>("n", async (_, _) => { entered.TrySetResult(true); await release.Task; });
        await client.ConnectAsync();
        var socket = await server.AcceptAsync();
        const string broadcast = "{\"id\":\"@\",\"action\":\"n\",\"data\":1}";
        await TestServer.SendAsync(socket, broadcast);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await TestServer.SendAsync(socket, broadcast);
        await TestServer.SendAsync(socket, broadcast);
        Assert.That(await error.Task.WaitAsync(TimeSpan.FromSeconds(5)), Is.TypeOf<DarkWsClientLimitException>());
        Assert.That(client.State, Is.EqualTo(DarkWsClientState.Disconnected));
        await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        release.TrySetResult(true);
    }

    [TestCase("{", WebSocketCloseStatus.ProtocolError)]
    [TestCase("[]", WebSocketCloseStatus.ProtocolError)]
    [TestCase("{\"id\":1}", WebSocketCloseStatus.ProtocolError)]
    [TestCase("{\"id\":\"x\",\"error\":null}", WebSocketCloseStatus.ProtocolError)]
    [TestCase("{\"id\":\"@\",\"data\":{}}", WebSocketCloseStatus.ProtocolError)]
    [TestCase("{\"id\":\"@\",\"data\":{\"action\":\"changed\"}}", WebSocketCloseStatus.ProtocolError)]
    [TestCase("{\"id\":\"@\",\"action\":null}", WebSocketCloseStatus.ProtocolError)]
    [TestCase("{\"id\":\"@\",\"action\":\" \"}", WebSocketCloseStatus.ProtocolError)]
    [TestCase("{\"id\":\"@\",\"action\":\"changed\",\"error\":\"failed\"}", WebSocketCloseStatus.ProtocolError)]
    public async Task InvalidWireMessagesCloseWithProtocolStatus(string text, WebSocketCloseStatus status) {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server);
        var error = Completion<Exception>();
        client.Error += (_, args) => error.TrySetResult(args.Exception);
        await client.ConnectAsync();
        var socket = await server.AcceptAsync();
        await TestServer.SendAsync(socket, text);
        Assert.That(await TestServer.ReadAsync(socket), Is.EqualTo("close"));
        Assert.That(socket.CloseStatus, Is.EqualTo(status));
        Assert.That(await error.Task.WaitAsync(TimeSpan.FromSeconds(5)), Is.TypeOf<DarkWsProtocolException>());
    }

    [TestCase(WebSocketMessageType.Binary, 100, WebSocketCloseStatus.InvalidMessageType)]
    [TestCase(WebSocketMessageType.Text, 1, WebSocketCloseStatus.MessageTooBig)]
    public async Task BinaryAndOversizedMessagesAreRejected(WebSocketMessageType type, int limit, WebSocketCloseStatus status) {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server, options => options.MaxMessageSizeBytes = limit);
        await client.ConnectAsync();
        var socket = await server.AcceptAsync();
        await socket.SendAsync(new byte[] { 65, 66 }, type, true, CancellationToken.None);
        Assert.That(await TestServer.ReadAsync(socket), Is.EqualTo("close"));
        Assert.That(socket.CloseStatus, Is.EqualTo(status));
    }

    [Test]
    public async Task FragmentedUtf8IsAssembledBeforeDecoding() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server);
        var response = client.RequestAsync<string>("unicode");
        var socket = await server.AcceptAsync();
        var request = await TestServer.RequestAsync(socket);
        var text = "{\"id\":\"" + request.GetProperty("id").GetString() + "\",\"data\":\"Привет\"}";
        foreach (var value in Encoding.UTF8.GetBytes(text).SkipLast(1))
            await socket.SendAsync(new byte[] { value }, WebSocketMessageType.Text, false, CancellationToken.None);
        await socket.SendAsync("}"u8.ToArray(), WebSocketMessageType.Text, true, CancellationToken.None);
        Assert.That(await response, Is.EqualTo("Привет"));
    }

    [Test]
    public async Task HeartbeatWaitsForPongAndDetectsUnresponsivePeer() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server, options => { options.PingInterval = TimeSpan.FromMilliseconds(40); options.PongTimeout = TimeSpan.FromMilliseconds(80); });
        var error = Completion<Exception>();
        client.Error += (_, args) => error.TrySetResult(args.Exception);
        await client.ConnectAsync();
        var socket = await server.AcceptAsync();
        Assert.That(await TestServer.ReadAsync(socket), Is.EqualTo("ping"));
        await TestServer.SendAsync(socket, "pong");
        Assert.That(await TestServer.ReadAsync(socket), Is.EqualTo("ping"));
        Assert.That(await error.Task.WaitAsync(TimeSpan.FromSeconds(5)), Is.TypeOf<DarkWsConnectionException>());
    }

    [TestCase(401)]
    [TestCase(403)]
    public async Task UpgradeAuthenticationFailuresStopTheCycle(int status) {
        await using var server = await TestServer.StartAsync(httpStatus: status);
        await using var client = Client(server, options => options.Reconnect = true);
        Assert.ThrowsAsync<DarkWsConnectionException>(() => client.ConnectAsync());
        Assert.That(client.State, Is.EqualTo(DarkWsClientState.Disconnected));
        Assert.That(server.Connections, Is.EqualTo(1));
    }

    [Test]
    public async Task ProviderAuthenticatesReconnectAndLogoutSuppressesIt() {
        await using var server = await TestServer.StartAsync(darkWs: true);
        var calls = 0;
        await using var client = Client(server, options => options.AuthenticationTokenProvider = _ => { Interlocked.Increment(ref calls); return ValueTask.FromResult<string?>("valid"); });
        Assert.That(await client.RequestAsync<int>("private:value"), Is.EqualTo(123));
        await client.CloseAsync();
        await client.ConnectAsync();
        Assert.That(calls, Is.EqualTo(2));
        await client.LogoutAsync();
        await client.CloseAsync();
        await client.ConnectAsync();
        Assert.That(calls, Is.EqualTo(2));
        Assert.ThrowsAsync<DarkWsResponseException>(() => client.RequestAsync("private:value"));
        await client.AuthenticateAsync("valid");
        await client.CloseAsync();
        await client.ConnectAsync();
        Assert.That(calls, Is.EqualTo(3));
    }

    [Test]
    public async Task SystemCommandsUseTextAndSerializeAcknowledgements() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server);
        var auth = client.AuthenticateAsync("private-token");
        var socket = await server.AcceptAsync();
        Assert.That(await TestServer.ReadAsync(socket), Is.EqualTo("auth:private-token"));
        var logout = client.LogoutAsync();
        await TestServer.SendAsync(socket, "logout:success");
        await TestServer.SendAsync(socket, "pong");
        var request = client.RequestAsync<int>("read");
        var json = await TestServer.RequestAsync(socket);
        Assert.That(json.GetProperty("action").GetString(), Is.EqualTo("read"));
        await TestServer.ReplyAsync(socket, json);
        Assert.That(await request, Is.EqualTo(42));
        Assert.That(auth.IsCompleted, Is.False);
        Assert.That(logout.IsCompleted, Is.False);
        await TestServer.SendAsync(socket, "auth:success");
        await auth;
        Assert.That(await TestServer.ReadAsync(socket), Is.EqualTo("logout"));
        await TestServer.SendAsync(socket, "logout:success");
        await logout;
        var rejected = client.AuthenticateAsync("expired");
        Assert.That(await TestServer.ReadAsync(socket), Is.EqualTo("auth:expired"));
        await TestServer.SendAsync(socket, "auth:failed");
        var error = Assert.ThrowsAsync<DarkWsResponseException>(async () => await rejected)!;
        Assert.That(error.Code, Is.EqualTo("auth:failed"));
        Assert.That(error.RequestId, Is.Empty);
        Assert.That(error.Action, Is.EqualTo("auth"));
        Assert.That(error.Message, Does.Not.Contain("expired"));
        Assert.That(client.State, Is.EqualTo(DarkWsClientState.Connected));
        await TestServer.SendAsync(socket, "auth:success");
        var nextRequest = client.RequestAsync<int>("read");
        await TestServer.ReplyAsync(socket, await TestServer.RequestAsync(socket));
        Assert.That(await nextRequest, Is.EqualTo(42));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task UnacknowledgedSystemCommandDiscardsConnection(bool cancel) {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server, options => options.RequestTimeout = TimeSpan.FromMilliseconds(150));
        using var cancellation = new CancellationTokenSource();
        var auth = client.AuthenticateAsync("valid", cancellation.Token);
        var socket = await server.AcceptAsync();
        Assert.That(await TestServer.ReadAsync(socket), Is.EqualTo("auth:valid"));
        if (cancel) {
            cancellation.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () => await auth);
        } else {
            Assert.That(Assert.ThrowsAsync<DarkWsTimeoutException>(async () => await auth)!.Stage, Is.EqualTo(DarkWsTimeoutStage.Response));
        }
        await client.CloseAsync();
        await client.ConnectAsync();
        var next = await server.AcceptAsync();
        var logout = client.LogoutAsync();
        Assert.That(await TestServer.ReadAsync(next), Is.EqualTo("logout"));
        await TestServer.SendAsync(next, "logout:success");
        await logout;
        Assert.That(server.Connections, Is.EqualTo(2));
    }

    [TestCase(null)]
    [TestCase("wrong")]
    public async Task InvalidProviderCredentialsFailReadiness(string? token) {
        await using var server = await TestServer.StartAsync(darkWs: true);
        await using var client = Client(server, options => { options.Reconnect = true; options.AuthenticationTokenProvider = _ => ValueTask.FromResult(token); });
        Assert.That(async () => await client.ConnectAsync(), Throws.Exception);
        Assert.That(client.State, Is.EqualTo(DarkWsClientState.Disconnected));
    }

    [Test]
    public async Task AutomaticAuthenticationKeepsConnectionTimeoutClassification() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server, options => {
            options.AuthenticationTokenProvider = _ => ValueTask.FromResult<string?>("valid");
            options.ConnectionTimeout = TimeSpan.FromMilliseconds(300);
            options.RequestTimeout = Timeout.InfiniteTimeSpan;
        });
        var failure = Completion<Exception>();
        client.Error += (_, args) => failure.TrySetResult(args.Exception);
        var connect = client.ConnectAsync();
        var socket = await server.AcceptAsync();
        Assert.That(await TestServer.ReadAsync(socket), Is.EqualTo("auth:valid"));
        Assert.That(Assert.ThrowsAsync<DarkWsTimeoutException>(async () => await connect)!.Stage, Is.EqualTo(DarkWsTimeoutStage.Connection));
        Assert.That(await failure.Task.WaitAsync(TimeSpan.FromSeconds(5)), Is.TypeOf<DarkWsTimeoutException>());
    }

    [Test]
    public async Task TransportFailureDuringAutomaticAuthenticationReconnects() {
        await using var server = await TestServer.StartAsync();
        var calls = 0;
        await using var client = Client(server, options => {
            options.Reconnect = true;
            options.ConnectionTimeout = TimeSpan.FromSeconds(10);
            options.RequestTimeout = Timeout.InfiniteTimeSpan;
            // The first call ends only when the dropped socket cancels it.
            options.AuthenticationTokenProvider = async token => {
                if (Interlocked.Increment(ref calls) == 1) await Task.Delay(Timeout.Infinite, token);
                return "valid";
            };
        });
        var failures = Channel.CreateUnbounded<Exception>();
        client.Error += (_, args) => failures.Writer.TryWrite(args.Exception);
        var connect = client.ConnectAsync();
        // Drops while the token is fetched and while auth:success is awaited both retry at once, long before ConnectionTimeout.
        await (await server.AcceptAsync()).CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "restarting", CancellationToken.None);
        var second = await server.AcceptAsync();
        Assert.That(await TestServer.ReadAsync(second), Is.EqualTo("auth:valid"));
        await second.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "restarting", CancellationToken.None);
        var third = await server.AcceptAsync();
        Assert.That(await TestServer.ReadAsync(third), Is.EqualTo("auth:valid"));
        await TestServer.SendAsync(third, "auth:success");
        await connect.WaitAsync(TimeSpan.FromSeconds(5));
        for (var i = 0; i < 2; i++) {
            var failure = (DarkWsConnectionException)await failures.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(failure.CloseStatus, Is.EqualTo(WebSocketCloseStatus.EndpointUnavailable));
        }
        Assert.That(client.State, Is.EqualTo(DarkWsClientState.Connected));
        Assert.That(calls, Is.EqualTo(3));
        Assert.That(server.Connections, Is.EqualTo(3));
    }

    [Test]
    public async Task DisposeAsyncClosesTheSocketGracefully() {
        await using var server = await TestServer.StartAsync();
        var client = Client(server);
        await client.ConnectAsync();
        var socket = await server.AcceptAsync();
        var read = TestServer.ReadAsync(socket);
        await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(await read, Is.EqualTo("close"));
        Assert.That(client.State, Is.EqualTo(DarkWsClientState.Disposed));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task QueuedSystemCommandThatNeverReachedTheSocketKeepsTheConnection(bool cancel) {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server, options => options.SendTimeout = TimeSpan.FromMilliseconds(200));
        await client.ConnectAsync();
        var socket = await server.AcceptAsync();
        // Loopback buffers absorb even very large writes here, so the test holds the send queue directly.
        var gate = SendGate(client);
        await gate.WaitAsync();
        using var cancellation = new CancellationTokenSource();
        var auth = client.AuthenticateAsync("token", cancellation.Token);
        if (cancel) {
            cancellation.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () => await auth);
        } else {
            Assert.That(Assert.ThrowsAsync<DarkWsTimeoutException>(async () => await auth)!.Stage, Is.EqualTo(DarkWsTimeoutStage.Send));
        }
        gate.Release();
        var next = client.RequestAsync<int>("next");
        var request = await TestServer.RequestAsync(socket);
        Assert.That(request.GetProperty("action").GetString(), Is.EqualTo("next"));
        await TestServer.ReplyAsync(socket, request);
        Assert.That(await next, Is.EqualTo(42));
        Assert.That(server.Connections, Is.EqualTo(1));
    }

    [Test]
    public async Task ConnectionFailuresKeepASanitizedCause() {
        await using var server = await TestServer.StartAsync(httpStatus: 500);
        await using var client = Client(server, options => options.Endpoint = new Uri(server.Endpoint + "?token=secret-token"));
        var failure = Assert.ThrowsAsync<DarkWsConnectionException>(() => client.ConnectAsync())!;
        Assert.That(failure.Message, Does.Contain("WebSocketError."));
        Assert.That(failure.ToString(), Does.Not.Contain("secret-token"));
    }

    [Test]
    public async Task ConnectionTimeoutAndDisposalDuringConnectAreBounded() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server, options => {
            options.ConnectionTimeout = TimeSpan.FromMilliseconds(80);
            options.ConfigureWebSocketOptionsAsync = async (_, token) => await Task.Delay(Timeout.Infinite, token);
        });
        Assert.That(Assert.ThrowsAsync<DarkWsTimeoutException>(() => client.ConnectAsync())!.Stage, Is.EqualTo(DarkWsTimeoutStage.Connection));
        await client.DisposeAsync();
        Assert.That(client.State, Is.EqualTo(DarkWsClientState.Disposed));
        Assert.ThrowsAsync<ObjectDisposedException>(() => client.ConnectAsync());
        Assert.Throws<ObjectDisposedException>(() => client.On("n", () => { }));
        Assert.ThrowsAsync<ObjectDisposedException>(() => client.CloseAsync());
        client.Dispose();
    }

    [Test]
    public void DiRejectsDuplicatesAndOwnsLazyInstances() {
        var services = new ServiceCollection();
        services.AddDarkWsClient(options => options.Endpoint = new Uri("ws://localhost/ws"));
        Assert.Throws<InvalidOperationException>(() => services.AddDarkWsClient(_ => { }));
        var count = services.Count;
        Assert.Throws<InvalidOperationException>(() => services.AddDarkWsClient((_, _) => { }));
        Assert.That(services, Has.Count.EqualTo(count));
        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IDarkWsClient>();
        provider.Dispose();
        Assert.That(client.State, Is.EqualTo(DarkWsClientState.Disposed));
        Assert.Throws<ArgumentNullException>(() => DarkWsClientServiceCollectionExtensions.AddDarkWsClient(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddDarkWsClient((Action<DarkWsClientOptions>)null!));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddDarkWsClient((Action<IServiceProvider, DarkWsClientOptions>)null!));
        var keyed = new ServiceCollection();
        keyed.AddKeyedSingleton<IDarkWsClient>("secondary", (_, _) => new DarkWsClient(new Uri("ws://localhost/other")));
        Assert.DoesNotThrow(() => keyed.AddDarkWsClient(options => options.Endpoint = new Uri("ws://localhost/ws")));
    }

    [Test]
    public void InvalidSettingsAndArgumentsFailEarly() {
        Assert.Throws<ArgumentNullException>(() => new DarkWsClient((DarkWsClientOptions)null!));
        foreach (var uri in new Uri?[] { null, new("relative", UriKind.Relative), new("https://host"), new("ws://user:password@host"), new("ws://host/#fragment") })
            Assert.Throws<ArgumentException>(() => new DarkWsClient(new DarkWsClientOptions { Endpoint = uri! }));
        foreach (var property in typeof(DarkWsClientOptions).GetProperties().Where(property => property.PropertyType == typeof(TimeSpan))) {
            var options = new DarkWsClientOptions { Endpoint = new Uri("ws://host") };
            property.SetValue(options, TimeSpan.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => new DarkWsClient(options));
        }
        foreach (var property in typeof(DarkWsClientOptions).GetProperties().Where(property => property.PropertyType == typeof(int))) {
            var options = new DarkWsClientOptions { Endpoint = new Uri("ws://host") };
            property.SetValue(options, 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => new DarkWsClient(options));
        }
        Assert.Throws<ArgumentNullException>(() => new DarkWsClient(new DarkWsClientOptions { Endpoint = new Uri("ws://host"), JsonOptions = null! }));
        using var client = new DarkWsClient(new Uri("ws://host"));
        Assert.Throws<ArgumentException>(() => client.On(" ", () => { }));
        Assert.Throws<ArgumentNullException>(() => client.On("n", null!));
        Assert.Throws<ArgumentNullException>(() => client.On<int>("n", null!));
        Assert.Throws<ArgumentNullException>(() => client.OnAsync<int>("n", null!));
        Assert.Throws<ArgumentException>(() => client.AuthenticateAsync(" "));
        Assert.ThrowsAsync<ArgumentException>(() => client.RequestAsync(" "));
    }

    [Test]
    public async Task ScopedRegistrationIsolatesSessionsAndDisposal() {
        await using var server = await TestServer.StartAsync(darkWs: true);
        var services = new ServiceCollection();
        services.AddScoped<IDarkWsClient>(_ => new DarkWsClient(server.Endpoint));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IDarkWsClient>();
        var second = secondScope.ServiceProvider.GetRequiredService<IDarkWsClient>();
        Assert.That(first, Is.Not.SameAs(second));
        await first.AuthenticateAsync("valid");
        Assert.That(await first.RequestAsync<int>("private:value"), Is.EqualTo(123));
        Assert.ThrowsAsync<DarkWsResponseException>(() => second.RequestAsync("private:value"));
        await firstScope.DisposeAsync();
        Assert.That(await second.RequestAsync<int>("echo:value", 3), Is.EqualTo(3));
    }

    [Test]
    public async Task SnapshotAndUnsubscriptionPreserveApplicationBoundaries() {
        await using var server = await TestServer.StartAsync(darkWs: true);
        var options = new DarkWsClientOptions { Endpoint = server.Endpoint };
        await using var client = new DarkWsClient(options);
        options.Endpoint = new Uri("ws://localhost:1/changed");
        options.JsonOptions.PropertyNamingPolicy = null;
        var result = await client.RequestAsync<JsonElement>("echo:value", new { MyValue = 12 });
        Assert.That(result.GetProperty("myValue").GetInt32(), Is.EqualTo(12));
        var called = 0;
        var subscription = client.On("changed", () => Interlocked.Increment(ref called));
        subscription.Dispose();
        await client.RequestAsync("echo:notify");
        Assert.That(called, Is.Zero);
        var observed = Completion<bool>();
        client.StateChanged += (_, _) => throw new InvalidOperationException("state observer");
        client.Error += (_, args) => { if (args.Exception.Message == "state observer") observed.TrySetResult(true); };
        await client.CloseAsync();
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task SubscriberConversionFailuresDoNotDiscardHealthyListeners() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server);
        var errors = System.Threading.Channels.Channel.CreateUnbounded<Exception>();
        var delivered = Completion<bool>();
        client.Error += (_, args) => errors.Writer.TryWrite(args.Exception);
        using var invalid = client.On<int>("n", _ => { });
        using var good = client.On("n", () => delivered.TrySetResult(true));
        await client.ConnectAsync();
        var socket = await server.AcceptAsync();
        await TestServer.SendAsync(socket, "{\"id\":\"@\",\"action\":\"n\"}");
        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(await errors.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)), Is.TypeOf<JsonException>());
        await TestServer.SendAsync(socket, "{\"id\":\"@\",\"action\":\"n\",\"data\":{}}");
        Assert.That(await errors.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)), Is.TypeOf<JsonException>());
    }

    [Test]
    public async Task PendingRequestsAndConnectWaitsTerminateWhenClosed() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server);
        var pending = client.RequestAsync("slow");
        var socket = await server.AcceptAsync();
        await TestServer.RequestAsync(socket);
        var close = client.CloseAsync();
        Assert.ThrowsAsync<DarkWsConnectionException>(async () => await pending);
        Assert.That(await TestServer.ReadAsync(socket), Is.EqualTo("close"));
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        await close;
        await client.CloseAsync();

        await using var blocked = Client(server, options => options.ConfigureWebSocketOptionsAsync = async (_, token) => await Task.Delay(Timeout.Infinite, token));
        var connecting = blocked.ConnectAsync();
        await blocked.CloseAsync();
        Assert.ThrowsAsync<DarkWsConnectionException>(async () => await connecting);
    }

    [Test]
    public async Task ConfigurationAndProviderExceptionsAreSanitizedAndTerminal() {
        await using var server = await TestServer.StartAsync(darkWs: true);
        await using var configured = Client(server, options => {
            options.Reconnect = true;
            options.ConfigureWebSocketOptionsAsync = (_, _) => throw new InvalidOperationException("secret token");
        });
        var failure = Assert.ThrowsAsync<DarkWsConnectionException>(() => configured.ConnectAsync())!;
        Assert.That(failure.ToString(), Does.Not.Contain("secret token"));
        await using var provider = Client(server, options => {
            options.AuthenticationTokenProvider = _ => throw new InvalidOperationException("secret token");
        });
        failure = Assert.ThrowsAsync<DarkWsConnectionException>(() => provider.ConnectAsync())!;
        Assert.That(failure.ToString(), Does.Not.Contain("secret token"));
    }

    [Test]
    public async Task WriteTimeoutClosesTheConnectionAndQueueWaitIsBounded() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server, options => options.SendTimeout = TimeSpan.FromMilliseconds(80));
        await client.ConnectAsync();
        _ = await server.AcceptAsync();
        // The peer deliberately never reads: this exceeds TCP buffers on supported test hosts.
        var large = client.RequestAsync("large", new string('x', 32 * 1024 * 1024));
        var small = client.RequestAsync("queued");
        var failure = Assert.CatchAsync<Exception>(async () => await large)!;
        Assert.That(failure, Is.TypeOf<DarkWsTimeoutException>());
        Assert.That(((DarkWsTimeoutException)failure).Stage, Is.EqualTo(DarkWsTimeoutStage.Send));
        Assert.CatchAsync<Exception>(async () => await small);
    }

    [Test]
    public async Task InvalidUtf8DoesNotLeaveAReadyConnection() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server, options => options.Reconnect = true);
        var error = Completion<Exception>();
        client.Error += (_, args) => error.TrySetResult(args.Exception);
        await client.ConnectAsync();
        var socket = await server.AcceptAsync();
        await socket.SendAsync(new byte[] { 0xff }, WebSocketMessageType.Text, true, CancellationToken.None);
        Assert.That(await TestServer.ReadAsync(socket), Is.EqualTo("close"));
        Assert.That(socket.CloseStatus, Is.EqualTo(WebSocketCloseStatus.InvalidPayloadData));
        _ = await error.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(client.State, Is.EqualTo(DarkWsClientState.Disconnected));
    }

    [Test]
    public async Task DisposalInterruptsAnAlreadyRunningGracefulClose() {
        await using var server = await TestServer.StartAsync();
        await using var client = Client(server, options => options.CloseTimeout = TimeSpan.FromMinutes(1));
        await client.ConnectAsync();
        var socket = await server.AcceptAsync();
        var closing = client.CloseAsync();
        Assert.That(await TestServer.ReadAsync(socket), Is.EqualTo("close"));
        await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        await closing.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(client.State, Is.EqualTo(DarkWsClientState.Disposed));
    }

    public sealed record Changed(int Value);
}
