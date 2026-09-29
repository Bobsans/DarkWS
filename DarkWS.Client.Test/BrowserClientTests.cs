using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using NUnit.Framework;

namespace DarkWS.Client.Test;

// The browser package, compiled from source, runs on Node.js's WHATWG WebSocket against a real DarkWS server.
[TestFixture]
public sealed class BrowserClientTests {
    private const string SCRIPT = """
        import DarkWs from "./index.js";

        const client = new DarkWs({
          host: process.argv[2], path: "/ws", secure: false, reconnect: false,
          authenticationToken: () => "valid",
        }).connect();
        const results = {};
        results.value = await client.request("echo:value", { n: 1 });
        results.empty = (await client.request("echo:empty")) ?? null;
        try { await client.request("echo:error"); } catch (error) { results.error = { code: error.message, data: error.data }; }
        const notified = new Promise(resolve => client.on("message", resolve));
        await client.request("echo:notify");
        results.notification = await notified;
        results.private = await client.request("private:value");
        client.dispose();
        console.log(JSON.stringify(results));
        """;

    private const string LIFECYCLE_SCRIPT = """
        import assert from "node:assert/strict";
        import DarkWs from "./index.js";

        let token = "expired";
        const client = new DarkWs({
          host: process.argv[2], path: "/ws", secure: false, reconnect: false,
          authenticationToken: () => token,
        });
        const events = [];
        client.on("sessionRestoreFailed", (error, event) => {
          events.push(["sessionRestoreFailed", error.message, client.isCurrentSocket(event)]);
        });
        const ready = new Promise(resolve => client.on("open", () => {
          events.push(["open"]);
          resolve();
        }));
        try {
          await assert.rejects(client.request("queued"), { message: "auth:failed" });
          await ready;
          assert.deepEqual(events, [["sessionRestoreFailed", "auth:failed", true], ["open"]]);
          assert.equal(await client.request("anonymous"), 42);

          const closed = new Promise(resolve => client.on("close", resolve));
          client.close();
          await closed;
          token = "valid";
          client.connect();
          const dropped = new Promise(resolve => client.on("close", resolve));
          await assert.rejects(client.request("drop"), { name: "ConnectionClosedError" });
          await dropped;
          // reconnect:false disables background retries; a new request still opens a socket.
          assert.equal(await client.request("recovered"), 42);
        } finally {
          client.dispose();
        }
        """;

    [Test]
    public async Task BrowserClientSpeaksTheServerProtocol() {
        await using var server = await TestServer.StartAsync(darkWs: true);
        using var results = JsonDocument.Parse(await RunBrowserAsync(SCRIPT, server));
        var root = results.RootElement;
        Assert.That(root.GetProperty("value").GetProperty("n").GetInt32(), Is.EqualTo(1));
        Assert.That(root.GetProperty("empty").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(root.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo("test:rejected"));
        Assert.That(root.GetProperty("error").GetProperty("data").GetInt32(), Is.EqualTo(7));
        Assert.That(root.GetProperty("notification").GetProperty("action").GetString(), Is.EqualTo("changed"));
        Assert.That(root.GetProperty("notification").GetProperty("data").GetProperty("value").GetInt32(), Is.EqualTo(11));
        Assert.That(root.GetProperty("private").GetInt32(), Is.EqualTo(123));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ClientsExposeRejectedRestorationAndDisabledReconnect(bool browser) {
        await using var server = await TestServer.StartAsync();
        var scenario = ServeScenarioAsync();
        var client = browser ? RunBrowserAsync(LIFECYCLE_SCRIPT, server) : RunDotNetLifecycleAsync(server);
        await Task.WhenAll(client, scenario);
        Assert.That(server.Connections, Is.EqualTo(3));

        async Task ServeScenarioAsync() {
            var rejected = await server.AcceptAsync();
            Assert.That(await TestServer.ReadAsync(rejected), Is.EqualTo("auth:expired"));
            await TestServer.SendAsync(rejected, "auth:failed");
            if (browser) {
                var anonymous = await TestServer.RequestAsync(rejected);
                Assert.That(anonymous.GetProperty("action").GetString(), Is.EqualTo("anonymous"));
                await TestServer.ReplyAsync(rejected, anonymous);
                Assert.That(await TestServer.ReadAsync(rejected), Is.EqualTo("close"));
                await rejected.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                server.FinishConnection(rejected);
            }

            var connected = await server.AcceptAsync();
            Assert.That(await TestServer.ReadAsync(connected), Is.EqualTo("auth:valid"));
            await TestServer.SendAsync(connected, "auth:success");
            var drop = await TestServer.RequestAsync(connected);
            Assert.That(drop.GetProperty("action").GetString(), Is.EqualTo("drop"));
            await connected.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "restarting", CancellationToken.None);
            if (browser) {
                Assert.That(await TestServer.ReadAsync(connected), Is.EqualTo("close"));
                server.FinishConnection(connected);
            }

            var recovered = await server.AcceptAsync();
            Assert.That(await TestServer.ReadAsync(recovered), Is.EqualTo("auth:valid"));
            await TestServer.SendAsync(recovered, "auth:success");
            var request = await TestServer.RequestAsync(recovered);
            Assert.That(request.GetProperty("action").GetString(), Is.EqualTo("recovered"));
            await TestServer.ReplyAsync(recovered, request);
            if (browser) {
                Assert.That(await TestServer.ReadAsync(recovered), Is.EqualTo("close"));
                await recovered.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                server.FinishConnection(recovered);
            }
        }
    }

    private static async Task RunDotNetLifecycleAsync(TestServer server) {
        var token = "expired";
        await using var client = new DarkWsClient(new DarkWsClientOptions {
            Endpoint = server.Endpoint,
            Reconnect = false,
            CloseTimeout = TimeSpan.FromMilliseconds(100),
            AuthenticationTokenProvider = _ => ValueTask.FromResult<string?>(token)
        });
        var disconnected = Channel.CreateUnbounded<DarkWsStateChangedEventArgs>();
        client.StateChanged += (_, args) => {
            if (args.State == DarkWsClientState.Disconnected) {
                disconnected.Writer.TryWrite(args);
            }
        };

        Assert.That((await Assert.ThrowsAsync<DarkWsResponseException>(() => client.ConnectAsync()))!.Code, Is.EqualTo("auth:failed"));
        var rejected = await disconnected.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(rejected.Reason, Is.TypeOf<DarkWsResponseException>());
        await Assert.ThrowsAsync<DarkWsConnectionException>(() => client.RequestAsync("anonymous"));
        Assert.That(server.Connections, Is.EqualTo(1));

        token = "valid";
        await client.ConnectAsync();
        await Assert.ThrowsAsync<DarkWsConnectionException>(() => client.RequestAsync("drop"));
        await disconnected.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<DarkWsConnectionException>(() => client.RequestAsync("recovered"));
        Assert.That(server.Connections, Is.EqualTo(2));
        await client.ConnectAsync();
        Assert.That(await client.RequestAsync<int>("recovered"), Is.EqualTo(42));
    }

    private static async Task<string> RunBrowserAsync(string source, TestServer server) {
        var package = Path.Combine(RepositoryRoot(), "packages", "darkws");
        var compiler = Path.Combine(package, "node_modules", "typescript", "bin", "tsc");
        if (!File.Exists(compiler)) {
            const string message = "Run npm ci --prefix packages/darkws to install the browser package tools.";
            // A skipped cross-language contract must not turn CI green.
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)) {
                Assert.Fail(message);
            }

            Assert.Ignore(message);
        }

        var output = Directory.CreateTempSubdirectory("darkws-browser-");
        try {
            await NodeAsync(compiler, "-p", Path.Combine(package, "tsconfig.json"), "--outDir", output.FullName,
                "--declaration", "false", "--declarationMap", "false", "--sourceMap", "false");
            await File.WriteAllTextAsync(Path.Combine(output.FullName, "package.json"), """{ "type": "module" }""");
            var script = Path.Combine(output.FullName, "contract.mjs");
            await File.WriteAllTextAsync(script, source);
            return await NodeAsync(script, server.Endpoint.Authority);
        } finally {
            output.Delete(recursive: true);
        }
    }

    private static async Task<string> NodeAsync(params string[] arguments) {
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try {
            await process.WaitForExitAsync(timeout.Token);
        } catch (OperationCanceledException) {
            process.Kill(entireProcessTree: true);
            throw;
        }

        Assert.That(process.ExitCode, Is.Zero, await error);
        return await output;
    }

    private static string RepositoryRoot() {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) {
            if (File.Exists(Path.Combine(directory.FullName, "DarkWS.sln"))) {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("DarkWS.sln was not found above the test output directory");
    }
}
