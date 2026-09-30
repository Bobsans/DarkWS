using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

internal static class DocsSmoke {
    internal static async Task RunAsync(WebApplication app, bool chat, bool legacy) {
        app.Urls.Clear();
        app.Urls.Add("http://127.0.0.1:0");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try {
            await app.StartAsync(deadline.Token);
            var endpoint = new UriBuilder(app.Urls.Single()) { Scheme = "ws", Path = "/ws" }.Uri;
            if (chat) {
                var start = new ProcessStartInfo("node") {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                start.ArgumentList.Add(Environment.GetEnvironmentVariable("DARKWS_DOCS_NODE_SCRIPT")!);
                start.ArgumentList.Add(Environment.GetEnvironmentVariable("DARKWS_DOCS_SDK")!);
                start.ArgumentList.Add(endpoint.Authority);
                start.ArgumentList.Add(legacy ? "4" : "current");
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                try {
                    await process.WaitForExitAsync(deadline.Token);
                    Console.WriteLine(await output);
                    if (process.ExitCode != 0) {
                        throw new InvalidOperationException(await error);
                    }
                } finally {
                    if (!process.HasExited) {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync();
                    }
                }
            } else {
                using var socket = new ClientWebSocket();
                await socket.ConnectAsync(endpoint, deadline.Token);
                await socket.SendAsync(Encoding.UTF8.GetBytes("""{"id":"smoke","action":"math:sum","data":{"left":2,"right":3}}"""), WebSocketMessageType.Text, true, deadline.Token);
                var buffer = new byte[4096];
                using var response = new MemoryStream();
                WebSocketReceiveResult part;
                do {
                    part = await socket.ReceiveAsync(buffer, deadline.Token);
                    response.Write(buffer, 0, part.Count);
                } while (!part.EndOfMessage);
                using var json = JsonDocument.Parse(response.ToArray());
                if (json.RootElement.GetProperty("data").GetProperty("value").GetInt32() != 5) {
                    throw new InvalidOperationException("The documented math:sum example returned an incorrect result.");
                }
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, deadline.Token);
            }
        } finally {
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await app.StopAsync(shutdown.Token);
            await app.DisposeAsync();
        }
    }
}
