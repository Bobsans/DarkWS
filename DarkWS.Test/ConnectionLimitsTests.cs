using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DarkWS.Abstractions;
using DarkWS.Test.Project;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace DarkWS.Test;

public sealed class ConnectionLimitsTests {
    [TestCase(1, false, false)]
    [TestCase(4, false, false)]
    [TestCase(16, false, false)]
    [TestCase(1, true, false)]
    [TestCase(1, false, true)]
    [TestCase(4, false, true)]
    [TestCase(16, false, true)]
    [TestCase(1, true, true)]
    public async Task RequestsAreBoundedPerConnectionAndWaitingCanBeCancelled(int limit, bool cancel, bool runOnThreadPool) {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDarkWs(options => {
            options.MaxConcurrentRequestsPerConnection = limit;
            options.RunActionsOnThreadPool = runOnThreadPool;
        });
        services.AddSingleton<RequestProbe>();
        services.AddScoped<SlowHandler>();
        await using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<DarkWsActionRegistry>().Add(typeof(SlowHandler));
        var probe = provider.GetRequiredService<RequestProbe>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var sockets = new[] { new TestWebSocket(), new TestWebSocket() };
        var accepts = new List<Task>();
        foreach (var socket in sockets) {
            for (var id = 0; id < limit; id++) {
                socket.EnqueueReceive($$"""{"id":"{{id}}","action":"limits:slow"}""");
            }

            var connection = new WebSocketConnection(socket, new DefaultHttpContext { RequestServices = provider }, null);
            accepts.Add(provider.GetRequiredService<WebSocketHandler>().AcceptAsync(connection, cancellation.Token));
        }

        try {
            await UntilAsync(() => probe.Started == limit * 2);
            foreach (var socket in sockets) {
                for (var id = limit; id <= 2 * limit; id++) {
                    socket.EnqueueReceive($$"""{"id":"{{id}}","action":"limits:slow"}""");
                }
            }
            // Admission can wait for a queue place while the transport already waits for the next frame.
            await UntilAsync(() => sockets.All(socket => socket.ReceiveCount == 2 * limit + 2));
            Assert.That(probe.Started, Is.EqualTo(limit * 2));
        } finally {
            if (cancel) {
                cancellation.Cancel();
            }

            probe.Release.TrySetResult();
            if (!cancel) {
                await Task.WhenAll(sockets.Select(async socket => {
                    await UntilAsync(() => socket.Sent.Count == 2 * limit + 1);
                    // Pace subsequent batches; a peer flooding the intake is tested separately below.
                    for (var first = 2 * limit + 1; first < 500; first += limit) {
                        var end = Math.Min(500, first + limit);
                        for (var id = first; id < end; id++) {
                            socket.EnqueueReceive($$"""{"id":"{{id}}","action":"limits:slow"}""");
                        }
                        await UntilAsync(() => socket.Sent.Count == end);
                    }
                }));

                foreach (var socket in sockets) {
                    socket.EnqueueClose();
                }
            }

            await Task.WhenAll(accepts).WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.That(probe.Peak, Is.LessThanOrEqualTo(limit * 2));
        Assert.That(provider.GetRequiredService<ConnectionStorage>().GetAll(), Is.Empty);
        if (!cancel) {
            foreach (var socket in sockets) {
                var ids = socket.Sent.Select(bytes => JsonSerializer.Deserialize<ResponseMessageNoData>(bytes, Utils.JsonOptions)!.Id);
                Assert.That(ids, Is.EquivalentTo(Enumerable.Range(0, 500).Select(id => id.ToString())));
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SynchronousHandlerOnlyBlocksDispatchByDefault(bool runOnThreadPool) {
        await using var provider = CreateProvider(options => {
            options.MaxConcurrentRequestsPerConnection = 2;
            if (runOnThreadPool) {
                options.RunActionsOnThreadPool = true;
            }
        });
        var probe = provider.GetRequiredService<RequestProbe>();
        var socket = new TestWebSocket();
        var session = new TestSession("original", new ClaimsPrincipal(new ClaimsIdentity([], "Test")));
        var connection = new WebSocketConnection(socket, new DefaultHttpContext { RequestServices = provider }, session);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(connection, cancellation.Token);
        try {
            socket.EnqueueReceive("""{"id":"blocked","action":"limits:blocking"}""");
            await UntilAsync(() => probe.Started == 1);
            socket.EnqueueReceive("""{"id":"fast","action":"limits:fast"}""");
            socket.EnqueueReceive("auth:");
            socket.EnqueueReceive("logout");
            if (runOnThreadPool) {
                await UntilAsync(() => socket.Sent.Count == 3);
                Assert.That(connection.Session, Is.Null);
            } else {
                await Task.Delay(100);
                Assert.That(socket.Sent, Is.Empty);
            }

            probe.Release.TrySetResult();
            await UntilAsync(() => socket.Sent.Count == 4);
            Assert.That(socket.Sent.Select(Encoding.UTF8.GetString), Is.EquivalentTo(new[] {
                "{\"id\":\"blocked\",\"data\":\"original\"}", "{\"id\":\"fast\"}", "auth:failed", "logout:success"
            }));
        } finally {
            probe.Release.TrySetResult();
            cancellation.Cancel();
            await accept.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Test]
    public async Task SaturatedConnectionKeepsReadingAndAnswersPing() {
        await using var provider = CreateProvider(options => options.MaxConcurrentRequestsPerConnection = 1);
        var probe = provider.GetRequiredService<RequestProbe>();
        var socket = new TestWebSocket();
        socket.EnqueueReceive("""{"id":"1","action":"limits:slow"}""");
        socket.EnqueueReceive("""{"id":"2","action":"limits:slow"}""");
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(new WebSocketConnection(socket, new DefaultHttpContext { RequestServices = provider }, null));
        await UntilAsync(() => probe.Started == 1);
        // The only request slot stays busy, yet the reader still receives and answers ping.
        socket.EnqueueReceive("ping");
        await UntilAsync(() => socket.Sent.Any(bytes => Encoding.UTF8.GetString(bytes) == "pong"));
        Assert.That(probe.Started, Is.EqualTo(1));
        probe.Release.TrySetResult();
        await UntilAsync(() => socket.Sent.Count == 3);
        socket.EnqueueClose();
        await accept.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task FullOrdinaryQueueKeepsReadingPingAndPeerClose(bool blockBusyWrite) {
        await using var provider = CreateProvider(options => {
            options.MaxConcurrentRequestsPerConnection = 1;
            options.RequestQueueTimeout = blockBusyWrite ? TimeSpan.FromMilliseconds(50) : TimeSpan.FromSeconds(5);
            options.ShutdownTimeout = TimeSpan.FromMilliseconds(100);
        });
        var probe = provider.GetRequiredService<RequestProbe>();
        var socket = new TestWebSocket();
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(new WebSocketConnection(socket, new DefaultHttpContext { RequestServices = provider }, null));
        socket.EnqueueReceive("""{"id":"1","action":"limits:slow"}""");
        await UntilAsync(() => probe.Started == 1);
        socket.EnqueueReceive("""{"id":"2","action":"limits:slow"}""");
        await UntilAsync(() => socket.ReceiveCount == 3);
        if (blockBusyWrite) {
            socket.SendBarrier = new TaskCompletionSource().Task;
        }
        socket.EnqueueReceive("""{"id":"3","action":"limits:slow"}""");
        await UntilAsync(() => socket.ReceiveCount == 4);
        if (blockBusyWrite) {
            await socket.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        socket.EnqueueReceive("ping");
        await UntilAsync(() => socket.ReceiveCount == 5, 1000);
        if (!blockBusyWrite) {
            await UntilAsync(() => socket.Sent.Any(bytes => Encoding.UTF8.GetString(bytes) == "pong"), 1000);
        }
        socket.EnqueueClose();
        await accept.WaitAsync(TimeSpan.FromSeconds(2));
        await UntilAsync(() => socket.WasDisposed);
        Assert.That(probe.Active, Is.Zero);
        Assert.That(provider.GetRequiredService<ConnectionStorage>().GetAll(), Is.Empty);
    }

    [Test]
    public async Task AdmissionFloodClosesWithBoundedWork() {
        await using var provider = CreateProvider(options => {
            options.MaxConcurrentRequestsPerConnection = 1;
            options.RequestQueueTimeout = TimeSpan.FromSeconds(5);
        });
        var probe = provider.GetRequiredService<RequestProbe>();
        var socket = new TestWebSocket();
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(new WebSocketConnection(socket, new DefaultHttpContext { RequestServices = provider }, null));
        socket.EnqueueReceive("""{"id":"1","action":"limits:slow"}""");
        await UntilAsync(() => probe.Started == 1);
        socket.EnqueueReceive("""{"id":"2","action":"limits:slow"}""");
        await UntilAsync(() => socket.ReceiveCount == 3);
        socket.EnqueueReceive("""{"id":"3","action":"limits:slow"}""");
        await UntilAsync(() => socket.ReceiveCount == 4);
        for (var id = 4; id < 30; id++) {
            socket.EnqueueReceive($$"""{"id":"{{id}}","action":"limits:slow"}""");
        }
        await accept.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(socket.LastOutputCloseStatus, Is.EqualTo(WebSocketCloseStatus.PolicyViolation));
        Assert.That(socket.ReceiveCount, Is.LessThanOrEqualTo(10));
        Assert.That(probe.Peak, Is.EqualTo(1));
        Assert.That(provider.GetRequiredService<ConnectionStorage>().GetAll(), Is.Empty);
    }

    [Test]
    public async Task RequestWaitingLongerThanTheQueueTimeoutIsRejectedAsBusy() {
        await using var provider = CreateProvider(options => {
            options.MaxConcurrentRequestsPerConnection = 1;
            options.RequestQueueTimeout = TimeSpan.FromMilliseconds(100);
        });
        var probe = provider.GetRequiredService<RequestProbe>();
        var socket = new TestWebSocket();
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(new WebSocketConnection(socket, new DefaultHttpContext { RequestServices = provider }, null));
        // Request 1 runs, request 2 fills the one-place queue, request 3 gives up after the timeout.
        socket.EnqueueReceive("""{"id":"1","action":"limits:slow"}""");
        await UntilAsync(() => probe.Started == 1);
        socket.EnqueueReceive("""{"id":"2","action":"limits:slow"}""");
        await UntilAsync(() => socket.ReceiveCount == 3);
        socket.EnqueueReceive("""{"id":"3","action":"limits:slow"}""");
        await UntilAsync(() => socket.Sent.Count == 1);
        Assert.That(JsonSerializer.Deserialize<ErrorMessage>(socket.Sent.Single(), Utils.JsonOptions), Is.EqualTo(new ErrorMessage("3", "darkws:error:busy")));
        Assert.That(probe.Started, Is.EqualTo(1));
        probe.Release.TrySetResult();
        await UntilAsync(() => socket.Sent.Count == 3);
        socket.EnqueueClose();
        await accept.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestCase("logout", "logout:success")]
    [TestCase("auth:token", "auth:failed")]
    public async Task CommandAtFullRequestQueueDoesNotBlockPing(string command, string reply) {
        await using var provider = CreateProvider(options => {
            options.MaxConcurrentRequestsPerConnection = 1;
            options.RequestQueueTimeout = TimeSpan.FromMilliseconds(100);
        });
        var probe = provider.GetRequiredService<RequestProbe>();
        var socket = new TestWebSocket();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(
            new WebSocketConnection(socket, new DefaultHttpContext { RequestServices = provider }, null), cancellation.Token);
        try {
            socket.EnqueueReceive("""{"id":"1","action":"limits:slow"}""");
            socket.EnqueueReceive("""{"id":"2","action":"limits:slow"}""");
            await UntilAsync(() => probe.Started == 1 && socket.ReceiveCount == 3);
            socket.EnqueueReceive(command);
            socket.EnqueueReceive("ping");
            await UntilAsync(() => socket.Sent.Count > 0, 1000);
            Assert.That(socket.Sent.Select(Encoding.UTF8.GetString), Is.EqualTo(new[] { "pong" }));
            Assert.That(probe.Started, Is.EqualTo(1));
            probe.Release.TrySetResult();
            await UntilAsync(() => socket.Sent.Count == 4);
            Assert.That(socket.Sent.Select(Encoding.UTF8.GetString), Is.EquivalentTo(new[] { "pong", "{\"id\":\"1\"}", "{\"id\":\"2\"}", reply }));
        } finally {
            probe.Release.TrySetResult();
            cancellation.Cancel();
            await accept.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Test]
    public async Task CommandsWaitForAQueuePlaceInsteadOfBeingRejected() {
        await using var provider = CreateProvider(options => {
            options.MaxConcurrentRequestsPerConnection = 1;
            options.RequestQueueTimeout = TimeSpan.FromMilliseconds(50);
        });
        var probe = provider.GetRequiredService<RequestProbe>();
        var socket = new TestWebSocket();
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(new WebSocketConnection(socket, new DefaultHttpContext { RequestServices = provider }, null));
        socket.EnqueueReceive("""{"id":"1","action":"limits:slow"}""");
        await UntilAsync(() => probe.Started == 1);
        socket.EnqueueReceive("""{"id":"2","action":"limits:slow"}""");
        await UntilAsync(() => socket.ReceiveCount == 3);
        socket.EnqueueReceive("logout");
        await Task.Delay(200);
        Assert.That(socket.Sent, Is.Empty);
        probe.Release.TrySetResult();
        await UntilAsync(() => socket.Sent.Count == 3);
        Assert.That(socket.Sent.Select(Encoding.UTF8.GetString), Is.EquivalentTo(new[] { "{\"id\":\"1\"}", "{\"id\":\"2\"}", "logout:success" }));
        socket.EnqueueClose();
        await accept.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestCase("logout", false)]
    [TestCase("auth:token", false)]
    [TestCase("logout", true)]
    public async Task CommandFloodClosesWithPolicyViolationAndBoundedOutput(string command, bool blockClose) {
        await using var provider = CreateProvider(options => options.MaxConcurrentRequestsPerConnection = 1);
        var probe = provider.GetRequiredService<RequestProbe>();
        var socket = new TestWebSocket();
        var connection = new WebSocketConnection(socket, new DefaultHttpContext { RequestServices = provider }, null) {
            SendTimeout = TimeSpan.FromMilliseconds(100)
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(connection, cancellation.Token);
        try {
            socket.EnqueueReceive("""{"id":"1","action":"limits:slow"}""");
            socket.EnqueueReceive("""{"id":"2","action":"limits:slow"}""");
            await UntilAsync(() => probe.Started == 1 && socket.ReceiveCount == 3);
            for (var index = 0; index < 4; index++) {
                socket.EnqueueReceive(command);
            }
            socket.EnqueueReceive("ping");
            await UntilAsync(() => socket.Sent.Count > 0, 1000);
            Assert.That(socket.Sent.Select(Encoding.UTF8.GetString), Is.EqualTo(new[] { "pong" }));
            Assert.That(connection.IsOpen, Is.True);
            if (blockClose) {
                socket.CloseBarrier = new TaskCompletionSource().Task;
            }
            socket.EnqueueReceive(command);
            if (!blockClose) {
                await UntilAsync(() => socket.LastOutputCloseStatus == WebSocketCloseStatus.PolicyViolation);
                socket.EnqueueClose();
            }
            await accept.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(socket.LastOutputCloseStatus, Is.EqualTo(blockClose ? null : (WebSocketCloseStatus?)WebSocketCloseStatus.PolicyViolation));
            Assert.That(socket.WasAborted, Is.EqualTo(blockClose));
            Assert.That(probe.Active, Is.Zero);
            Assert.That(socket.WasDisposed, Is.True);
            Assert.That(provider.GetRequiredService<ConnectionStorage>().GetAll(), Is.Empty);
        } finally {
            probe.Release.TrySetResult();
            cancellation.Cancel();
            await accept.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Test]
    public async Task RequestTimesOutWhenCommandsFillSharedQueue() {
        await using var provider = CreateProvider(options => {
            options.MaxConcurrentRequestsPerConnection = 2;
            options.RequestQueueTimeout = TimeSpan.FromMilliseconds(100);
        });
        var probe = provider.GetRequiredService<RequestProbe>();
        var socket = new TestWebSocket();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var accept = provider.GetRequiredService<WebSocketHandler>().AcceptAsync(
            new WebSocketConnection(socket, new DefaultHttpContext { RequestServices = provider }, null), cancellation.Token);
        try {
            for (var id = 1; id <= 3; id++) {
                socket.EnqueueReceive($$"""{"id":"{{id}}","action":"limits:slow"}""");
            }
            await UntilAsync(() => probe.Started == 2 && socket.ReceiveCount == 4);
            // One queued request and five commands fill capacity 2 + 4, leaving one request permit unused.
            for (var index = 0; index < 5; index++) {
                socket.EnqueueReceive("logout");
            }
            socket.EnqueueReceive("ping");
            await UntilAsync(() => socket.Sent.Count > 0, 1000);
            socket.EnqueueReceive("""{"id":"4","action":"limits:slow"}""");
            await UntilAsync(() => socket.Sent.Count == 2);
            Assert.That(socket.Sent.Select(Encoding.UTF8.GetString), Is.EqualTo(new[] { "pong", "{\"id\":\"4\",\"error\":\"darkws:error:busy\"}" }));
            probe.Release.TrySetResult();
            await UntilAsync(() => socket.Sent.Count == 10);
            Assert.That(probe.Started, Is.EqualTo(3));
            Assert.That(probe.Peak, Is.LessThanOrEqualTo(2));
        } finally {
            probe.Release.TrySetResult();
            cancellation.Cancel();
            await accept.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestCase(null, 100)]
    [TestCase("logout", 100)]
    [TestCase("auth:token", 100)]
    [TestCase(null, 5000)]
    [TestCase("logout", 5000)]
    [TestCase("auth:token", 5000)]
    public async Task SaturatedConnectionSurvivesTransportKeepAlive(string? command, int queueTimeout) {
        using var host = await new HostBuilder().ConfigureWebHost(builder => builder
            .UseKestrel(options => options.Listen(IPAddress.Loopback, 0))
            .ConfigureServices(services => {
                services.AddRouting();
                services.AddSingleton<RequestProbe>();
                services.AddScoped<SlowHandler>();
                // The long admission timeout exceeds both the PONG deadline and the saturation window.
                services.AddDarkWs(options => {
                    options.MaxConcurrentRequestsPerConnection = 1;
                    options.RequestQueueTimeout = TimeSpan.FromMilliseconds(queueTimeout);
                    options.KeepAliveInterval = TimeSpan.FromMilliseconds(100);
                    options.KeepAliveTimeout = TimeSpan.FromMilliseconds(200);
                });
            })
            .Configure(app => {
                app.UseRouting();
                app.UseWebSockets();
                app.UseEndpoints(endpoints => endpoints.MapDarkWs());
            })).StartAsync();
        host.Services.GetRequiredService<DarkWsActionRegistry>().Add(typeof(SlowHandler));
        var address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(address.Replace("http://", "ws://", StringComparison.Ordinal) + "/ws"), CancellationToken.None);
        foreach (var id in new[] { "1", "2", "3" }) {
            await socket.SendTextAsync($$"""{"id":"{{id}}","action":"limits:slow"}""");
        }

        if (command is not null) {
            await socket.SendTextAsync(command);
        }
        _ = Task.Delay(TimeSpan.FromSeconds(1.5)).ContinueWith(_ => host.Services.GetRequiredService<RequestProbe>().Release.TrySetResult(), TaskScheduler.Default);
        var replies = new List<string>();
        // A pending receive also answers the server's keep-alive PINGs on this side.
        while (replies.Count < (command is null ? 3 : 4)) {
            replies.Add(Encoding.UTF8.GetString(await socket.ReceiveRawMessage()));
        }

        var expected = queueTimeout == 100
            ? new List<string> { "{\"id\":\"3\",\"error\":\"darkws:error:busy\"}", "{\"id\":\"1\"}", "{\"id\":\"2\"}" }
            : ["{\"id\":\"1\"}", "{\"id\":\"2\"}", "{\"id\":\"3\"}"];
        if (command is not null) {
            expected.Add(command == "logout" ? "logout:success" : "auth:failed");
        }
        Assert.That(replies, Is.EqualTo(expected));
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        await host.StopAsync();
    }

    [Test]
    public async Task ReceiveIdleTimeoutResetsForFragmentsAndMessagesThenAbortsSilence() {
        var socket = new TestWebSocket { ReceiveDelay = TimeSpan.FromMilliseconds(60) };
        for (var index = 0; index < 10; index++) {
            socket.EnqueueReceive("x", index == 9);
        }

        socket.EnqueueReceive("next");
        var receiveIdleTimeout = TimeSpan.FromMilliseconds(400);
        using var connection = new WebSocketConnection(socket, new DefaultHttpContext(), null) {
            ReceiveIdleTimeout = receiveIdleTimeout
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        Assert.That(Encoding.UTF8.GetString((await connection.ReceiveMessageAsync(timeout.Token)).Data), Is.EqualTo("xxxxxxxxxx"));
        Assert.That(Encoding.UTF8.GetString((await connection.ReceiveMessageAsync(timeout.Token)).Data), Is.EqualTo("next"));
        await Assert.ThrowsAsync<WebSocketException>(async () => await connection.ReceiveMessageAsync(timeout.Token));
        Assert.That(socket.WasAborted, Is.True);
    }

    [Test]
    public async Task CallerCancellationIsNotReportedAsIdleTimeout() {
        var socket = new TestWebSocket();
        var receiveIdleTimeout = TimeSpan.FromSeconds(10);
        using var connection = new WebSocketConnection(socket, new DefaultHttpContext(), null) {
            ReceiveIdleTimeout = receiveIdleTimeout
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.CatchAsync<OperationCanceledException>(async () => await connection.ReceiveMessageAsync(cancellation.Token));
        Assert.That(socket.WasAborted, Is.False);
    }

    [Test]
    public async Task UnresponsivePeerIsRemovedFromStorageByConfiguredTimeout() {
        var lifecycle = new ConnectionProbe();
        using var host = await new HostBuilder().ConfigureWebHost(builder => builder
            .UseKestrel(options => options.Listen(IPAddress.Loopback, 0))
            .ConfigureServices(services => {
                services.AddRouting();
                services.AddSingleton<DarkWsConnectionHooks>(lifecycle);
                services.AddDarkWs(options => {
                    options.KeepAliveInterval = TimeSpan.FromMilliseconds(100);
                    options.KeepAliveTimeout = TimeSpan.FromMilliseconds(100);
                    options.ReceiveIdleTimeout = TimeSpan.FromMilliseconds(200);
                });
            })
            .Configure(app => {
                app.UseRouting();
                app.UseWebSockets();
                app.UseEndpoints(endpoints => endpoints.MapDarkWs());
            })).StartAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var address = new Uri(host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using var client = new TcpClient();
        await client.ConnectAsync(address.Host, address.Port, timeout.Token);
        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(
            $"GET /ws HTTP/1.1\r\nHost: {address.Authority}\r\nConnection: Upgrade\r\nUpgrade: websocket\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n\r\n"), timeout.Token);

        // Raw TCP deliberately never sends application traffic or answers WebSocket PINGs.
        await lifecycle.Opened.Task.WaitAsync(timeout.Token);
        await lifecycle.Closed.Task.WaitAsync(timeout.Token);
        var storage = host.Services.GetRequiredService<ConnectionStorage>();
        while (storage.GetAll().Count != 0) {
            await Task.Delay(10, timeout.Token);
        }

        Assert.That(storage.GetAll(), Is.Empty);
        await host.StopAsync(timeout.Token);
    }

    // Either the ASP.NET WebSocket middleware or DarkWsOptions.AllowedOrigins rejects cross-site upgrades.
    [TestCase(false)]
    [TestCase(true)]
    public async Task AllowedOriginsRejectCrossSiteUpgradesBeforeDarkWsRuns(bool darkWsOption) {
        var lifecycle = new ConnectionProbe();
        using var host = await new HostBuilder().ConfigureWebHost(builder => builder
            .UseKestrel(options => options.Listen(IPAddress.Loopback, 0))
            .ConfigureServices(services => {
                services.AddRouting();
                services.AddSingleton<DarkWsConnectionHooks>(lifecycle);
                services.AddDarkWs(options => {
                    if (darkWsOption) {
                        options.AllowedOrigins.Add("https://APP.example");
                    }
                });
            })
            .Configure(app => {
                app.UseRouting();
                app.UseWebSockets(darkWsOption ? new WebSocketOptions() : new WebSocketOptions { AllowedOrigins = { "https://app.example" } });
                app.UseEndpoints(endpoints => endpoints.MapDarkWs());
            })).StartAsync();
        var address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var endpoint = new Uri(address.Replace("http://", "ws://", StringComparison.Ordinal) + "/ws");

        async Task<HttpStatusCode> UpgradeAsync(string? origin) {
            using var socket = new ClientWebSocket();
            socket.Options.CollectHttpResponseDetails = true;
            if (origin is not null) {
                socket.Options.SetRequestHeader("Origin", origin);
            }

            try { await socket.ConnectAsync(endpoint, CancellationToken.None); } catch (WebSocketException) { }

            return socket.HttpStatusCode;
        }

        Assert.That(await UpgradeAsync("https://attacker.example"), Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(lifecycle.Opened.Task.IsCompleted, Is.False);
        Assert.That(await UpgradeAsync("https://app.example"), Is.EqualTo(HttpStatusCode.SwitchingProtocols));
        Assert.That(await UpgradeAsync(null), Is.EqualTo(HttpStatusCode.SwitchingProtocols));
        await host.StopAsync();
    }

    // HttpContext belongs to the upgrade request; once the connection has shut down, late reads fail deterministically.
    [Test]
    public async Task HandlerIgnoringCancellationCannotReadHttpContextAfterTheUpgradeRequestCompletes() {
        var probe = new LateContextProbe();
        using var host = await new HostBuilder().ConfigureWebHost(builder => builder
            .UseKestrel(options => options.Listen(IPAddress.Loopback, 0))
            .ConfigureServices(services => {
                services.AddRouting();
                services.AddSingleton(probe);
                services.AddDarkWs(options => options.ShutdownTimeout = TimeSpan.FromMilliseconds(100));
                services.AddScoped<LateContextHandler>();
                services.AddSingleton(provider => {
                    var registry = new DarkWsActionRegistry();
                    registry.Add(typeof(LateContextHandler));
                    return registry;
                });
            })
            .Configure(app => {
                app.UseRouting();
                app.UseWebSockets();
                app.UseEndpoints(endpoints => endpoints.MapDarkWs());
            })).StartAsync();
        var address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(address.Replace("http://", "ws://", StringComparison.Ordinal) + "/ws"), CancellationToken.None);
        await socket.SendAsync("{\"id\":\"1\",\"action\":\"late:read\"}"u8.ToArray(), WebSocketMessageType.Text, true, CancellationToken.None);
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        socket.Abort();
        await probe.RequestCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        probe.Release.TrySetResult();

        Assert.That(await probe.Result.Task.WaitAsync(TimeSpan.FromSeconds(5)), Is.InstanceOf<ObjectDisposedException>());
        await host.StopAsync();
    }

    public sealed class LateContextProbe {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource RequestCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Exception?> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    [Handler("late"), AllowAnonymous]
    public sealed class LateContextHandler(LateContextProbe probe) : HandlerBase {
        [Action("read")]
        public async Task<IResponse> ReadAsync() {
            HttpContext.Response.OnCompleted(() => {
                probe.RequestCompleted.TrySetResult();
                return Task.CompletedTask;
            });
            probe.Started.TrySetResult();
            await probe.Release.Task; // Deliberately ignores ConnectionAborted.
            try {
                _ = HttpContext.Request.Path;
                probe.Result.TrySetResult(null);
            } catch (Exception error) {
                probe.Result.TrySetResult(error);
            }

            return Ok();
        }
    }

    private static ServiceProvider CreateProvider(Action<DarkWsOptions> configure) {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDarkWs(configure);
        services.AddSingleton<RequestProbe>();
        services.AddScoped<SlowHandler>();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<DarkWsActionRegistry>().Add(typeof(SlowHandler));
        return provider;
    }

    private static async Task UntilAsync(Func<bool> condition, int timeoutMilliseconds = 5000) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMilliseconds));
        while (!condition()) {
            await Task.Delay(10, timeout.Token);
        }
    }

    public sealed class RequestProbe {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Started;
        public int Active;
        public int Peak;
    }

    [Handler("limits"), AllowAnonymous]
    public sealed class SlowHandler(RequestProbe probe) : HandlerBase {
        [Action("blocking")]
        public IResponse Blocking() {
            Interlocked.Increment(ref probe.Started);
            probe.Release.Task.WaitAsync(ConnectionAborted).GetAwaiter().GetResult();
            return Ok(Session.Id);
        }

        [Action("fast")]
        public IResponse Fast() => Ok();

        [Action("slow")]
        public async Task<IResponse> Slow() {
            Interlocked.Increment(ref probe.Started);
            InterlockedExtensions.Max(ref probe.Peak, Interlocked.Increment(ref probe.Active));
            try {
                await probe.Release.Task.WaitAsync(ConnectionAborted);
                return Ok();
            } finally {
                Interlocked.Decrement(ref probe.Active);
            }
        }
    }

    public sealed class ConnectionProbe : DarkWsConnectionHooks {
        public TaskCompletionSource Opened { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override Task OnOpenAsync(IDarkWsContextAccessor context) {
            Opened.TrySetResult();
            return Task.CompletedTask;
        }

        public override Task OnCloseAsync(IDarkWsContextAccessor context) {
            Closed.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
