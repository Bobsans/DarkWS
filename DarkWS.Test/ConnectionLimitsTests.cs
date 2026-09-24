using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
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
    [TestCase(1, false)]
    [TestCase(4, false)]
    [TestCase(16, false)]
    [TestCase(1, true)]
    public async Task RequestsAreBoundedPerConnectionAndWaitingCanBeCancelled(int limit, bool cancel) {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDarkWs(options => options.MaxConcurrentRequestsPerConnection = limit);
        services.AddSingleton<RequestProbe>();
        services.AddScoped<SlowHandler>();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<DarkWsActionRegistry>().Add(typeof(SlowHandler));
        var probe = provider.GetRequiredService<RequestProbe>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sockets = new[] { new TestWebSocket(), new TestWebSocket() };
        var accepts = new List<Task>();
        foreach (var socket in sockets) {
            for (var id = 0; id < 500; id++) {
                socket.EnqueueReceive($$"""{"id":"{{id}}","action":"limits:slow"}""");
            }
            var connection = new WebSocketConnection(socket, new DefaultHttpContext { RequestServices = provider }, null);
            accepts.Add(provider.GetRequiredService<WebSocketHandler>().AcceptAsync(connection, cancellation.Token));
        }

        try {
            await UntilAsync(() => probe.Started == limit * 2);
            // Each socket reads its running requests, as many queued ones, and one waiting for a free place.
            await UntilAsync(() => sockets.All(socket => socket.ReceiveCount == 2 * limit + 1));
            Assert.That(probe.Started, Is.EqualTo(limit * 2));
        } finally {
            if (cancel) cancellation.Cancel();
            probe.Release.TrySetResult();
            if (!cancel) {
                while (sockets.Any(socket => socket.Sent.Count < 500)) await Task.Delay(10, cancellation.Token);
                foreach (var socket in sockets) socket.EnqueueClose();
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

    [Test]
    public async Task SaturatedConnectionKeepsReadingAndAnswersPing() {
        using var provider = CreateProvider(options => options.MaxConcurrentRequestsPerConnection = 1);
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

    [Test]
    public async Task RequestWaitingLongerThanTheQueueTimeoutIsRejectedAsBusy() {
        using var provider = CreateProvider(options => {
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

    [Test]
    public async Task CommandsWaitForAQueuePlaceInsteadOfBeingRejected() {
        using var provider = CreateProvider(options => {
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

    [Test]
    public async Task SaturatedConnectionSurvivesTransportKeepAlive() {
        using var host = await new HostBuilder().ConfigureWebHost(builder => builder
            .UseKestrel(options => options.Listen(IPAddress.Loopback, 0))
            .ConfigureServices(services => {
                services.AddRouting();
                services.AddSingleton<RequestProbe>();
                services.AddScoped<SlowHandler>();
                // On .NET 9+ an unprocessed keep-alive PONG aborts after 600 ms, well inside the 1 s saturation.
                services.AddDarkWs(options => {
                    options.MaxConcurrentRequestsPerConnection = 1;
                    options.RequestQueueTimeout = TimeSpan.FromMilliseconds(100);
                    options.KeepAliveInterval = TimeSpan.FromMilliseconds(200);
                    options.KeepAliveTimeout = TimeSpan.FromMilliseconds(400);
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
        foreach (var id in new[] { "1", "2", "3" }) await socket.SendTextAsync($$"""{"id":"{{id}}","action":"limits:slow"}""");
        _ = Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(_ => host.Services.GetRequiredService<RequestProbe>().Release.TrySetResult(), TaskScheduler.Default);
        var replies = new List<string>();
        // A pending receive also answers the server's keep-alive PINGs on this side.
        while (replies.Count < 3) replies.Add(Encoding.UTF8.GetString(await socket.ReceiveRawMessage()));
        Assert.That(replies, Is.EqualTo(new[] { "{\"id\":\"3\",\"error\":\"darkws:error:busy\"}", "{\"id\":\"1\"}", "{\"id\":\"2\"}" }));
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        await host.StopAsync();
    }

    [Test]
    public async Task ReceiveIdleTimeoutResetsForFragmentsAndMessagesThenAbortsSilence() {
        var socket = new TestWebSocket { ReceiveDelay = TimeSpan.FromMilliseconds(60) };
        for (var index = 0; index < 10; index++) socket.EnqueueReceive("x", index == 9);
        socket.EnqueueReceive("next");
        using var connection = new WebSocketConnection(socket, new DefaultHttpContext(), null) {
            ReceiveIdleTimeout = TimeSpan.FromMilliseconds(400)
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        Assert.That(Encoding.UTF8.GetString((await connection.ReceiveMessageAsync(timeout.Token)).Data), Is.EqualTo("xxxxxxxxxx"));
        Assert.That(Encoding.UTF8.GetString((await connection.ReceiveMessageAsync(timeout.Token)).Data), Is.EqualTo("next"));
        Assert.ThrowsAsync<WebSocketException>(async () => await connection.ReceiveMessageAsync(timeout.Token));
        Assert.That(socket.WasAborted, Is.True);
    }

    [Test]
    public void CallerCancellationIsNotReportedAsIdleTimeout() {
        var socket = new TestWebSocket();
        using var connection = new WebSocketConnection(socket, new DefaultHttpContext(), null) {
            ReceiveIdleTimeout = TimeSpan.FromSeconds(10)
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await connection.ReceiveMessageAsync(cancellation.Token));
        Assert.That(socket.WasAborted, Is.False);
    }

    [Test]
    public async Task UnresponsivePeerIsRemovedFromStorageByConfiguredTimeout() {
        var lifecycle = new ConnectionProbe();
        using var host = await new HostBuilder().ConfigureWebHost(builder => builder
            .UseKestrel(options => options.Listen(IPAddress.Loopback, 0))
            .ConfigureServices(services => {
                services.AddRouting();
                services.AddSingleton<DarkWsMiddleware>(lifecycle);
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
        while (storage.GetAll().Count != 0) await Task.Delay(10, timeout.Token);
        Assert.That(storage.GetAll(), Is.Empty);
        await host.StopAsync(timeout.Token);
    }

    [Test]
    public async Task AllowedOriginsRejectCrossSiteUpgradesBeforeDarkWsRuns() {
        var lifecycle = new ConnectionProbe();
        using var host = await new HostBuilder().ConfigureWebHost(builder => builder
            .UseKestrel(options => options.Listen(IPAddress.Loopback, 0))
            .ConfigureServices(services => {
                services.AddRouting();
                services.AddSingleton<DarkWsMiddleware>(lifecycle);
                services.AddDarkWs();
            })
            .Configure(app => {
                app.UseRouting();
                app.UseWebSockets(new WebSocketOptions { AllowedOrigins = { "https://app.example" } });
                app.UseEndpoints(endpoints => endpoints.MapDarkWs());
            })).StartAsync();
        var address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var endpoint = new Uri(address.Replace("http://", "ws://", StringComparison.Ordinal) + "/ws");
        async Task<HttpStatusCode> UpgradeAsync(string? origin) {
            using var socket = new ClientWebSocket();
            socket.Options.CollectHttpResponseDetails = true;
            if (origin is not null) socket.Options.SetRequestHeader("Origin", origin);
            try { await socket.ConnectAsync(endpoint, CancellationToken.None); } catch (WebSocketException) { }
            return socket.HttpStatusCode;
        }

        Assert.That(await UpgradeAsync("https://attacker.example"), Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(lifecycle.Opened.Task.IsCompleted, Is.False);
        Assert.That(await UpgradeAsync("https://app.example"), Is.EqualTo(HttpStatusCode.SwitchingProtocols));
        Assert.That(await UpgradeAsync(null), Is.EqualTo(HttpStatusCode.SwitchingProtocols));
        await host.StopAsync();
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

    private static async Task UntilAsync(Func<bool> condition) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }

    public sealed class RequestProbe {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Started;
        public int Active;
        public int Peak;
    }

    [Handler("limits"), AllowAnonymous]
    public sealed class SlowHandler(RequestProbe probe) : HandlerBase {
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

    public sealed class ConnectionProbe : DarkWsMiddleware {
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
