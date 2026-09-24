using System.Diagnostics;
using System.Text.Json;
using NUnit.Framework;

namespace DarkWS.Client.Test;

// The browser package, compiled from source, runs on Node.js's WHATWG WebSocket against a real DarkWS server.
[TestFixture]
public sealed class BrowserClientTests {
    private const string Script = """
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

    [Test]
    public async Task BrowserClientSpeaksTheServerProtocol() {
        var package = Path.Combine(RepositoryRoot(), "packages", "darkws");
        var compiler = Path.Combine(package, "node_modules", "typescript", "bin", "tsc");
        if (!File.Exists(compiler)) Assert.Ignore("Run npm ci --prefix packages/darkws to install the browser package tools.");
        var output = Directory.CreateTempSubdirectory("darkws-browser-");
        try {
            await NodeAsync(compiler, "-p", Path.Combine(package, "tsconfig.json"), "--outDir", output.FullName,
                "--declaration", "false", "--declarationMap", "false", "--sourceMap", "false");
            await File.WriteAllTextAsync(Path.Combine(output.FullName, "package.json"), """{ "type": "module" }""");
            var script = Path.Combine(output.FullName, "contract.mjs");
            await File.WriteAllTextAsync(script, Script);
            await using var server = await TestServer.StartAsync(darkWs: true);

            using var results = JsonDocument.Parse(await NodeAsync(script, server.Endpoint.Authority));
            var root = results.RootElement;
            Assert.That(root.GetProperty("value").GetProperty("n").GetInt32(), Is.EqualTo(1));
            Assert.That(root.GetProperty("empty").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(root.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo("test:rejected"));
            Assert.That(root.GetProperty("error").GetProperty("data").GetInt32(), Is.EqualTo(7));
            Assert.That(root.GetProperty("notification").GetProperty("action").GetString(), Is.EqualTo("changed"));
            Assert.That(root.GetProperty("notification").GetProperty("data").GetProperty("value").GetInt32(), Is.EqualTo(11));
            Assert.That(root.GetProperty("private").GetInt32(), Is.EqualTo(123));
        } finally {
            output.Delete(recursive: true);
        }
    }

    private static async Task<string> NodeAsync(params string[] arguments) {
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
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
            if (File.Exists(Path.Combine(directory.FullName, "DarkWS.sln"))) return directory.FullName;
        }
        throw new InvalidOperationException("DarkWS.sln was not found above the test output directory");
    }
}
