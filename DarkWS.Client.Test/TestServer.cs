using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using DarkWS.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DarkWS.Client.Test;

internal sealed class TestServer : IAsyncDisposable {
    private readonly WebApplication _app;
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentBag<WebSocket> _sockets = [];
    private readonly Channel<WebSocket> _accepted = Channel.CreateUnbounded<WebSocket>();
    internal Uri Endpoint { get; private set; } = null!;
    internal int Connections;
    private TestServer(WebApplication app) => _app = app;

    internal static async Task<TestServer> StartAsync(bool darkWs = false, int? httpStatus = null) {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        if (darkWs) builder.Services.AddDarkWs().AddHandlersFromAssemblyContaining<EchoHandler>().AddAuthenticator<TestAuthenticator, TestAuthenticator.TestSession>();
        var app = builder.Build();
        var server = new TestServer(app);
        app.UseWebSockets();
        app.Use(async (context, next) => {
            Interlocked.Increment(ref server.Connections);
            await next(context);
        });
        if (darkWs) app.MapDarkWs("/ws");
        else app.Map("/ws", async context => {
            if (httpStatus.HasValue) { context.Response.StatusCode = httpStatus.Value; return; }
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            server._sockets.Add(socket);
            server._accepted.Writer.TryWrite(socket);
            try { await Task.Delay(Timeout.Infinite, server._stop.Token); }
            catch (OperationCanceledException) { }
        });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        server.Endpoint = new Uri(address.Replace("http://", "ws://", StringComparison.Ordinal) + "/ws");
        return server;
    }

    internal async Task<WebSocket> AcceptAsync() => await _accepted.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    internal static async Task<string> ReadAsync(WebSocket socket) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        WebSocketReceiveResult result;
        do {
            result = await socket.ReceiveAsync(buffer, timeout.Token);
            if (result.MessageType == WebSocketMessageType.Close) return "close";
            output.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(output.ToArray());
    }
    internal static Task SendAsync(WebSocket socket, string value) => socket.SendAsync(Encoding.UTF8.GetBytes(value), WebSocketMessageType.Text, true, CancellationToken.None);
    internal static async Task<JsonElement> RequestAsync(WebSocket socket) {
        using var document = JsonDocument.Parse(await ReadAsync(socket));
        return document.RootElement.Clone();
    }
    internal static Task ReplyAsync(WebSocket socket, JsonElement request, string fields = "\"data\":42") =>
        SendAsync(socket, "{\"id\":\"" + request.GetProperty("id").GetString() + "\"" + (fields.Length > 0 ? "," + fields : "") + "}");
    public async ValueTask DisposeAsync() {
        foreach (var socket in _sockets) socket.Abort();
        _stop.Cancel();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _app.StopAsync(timeout.Token);
        await _app.DisposeAsync();
        _stop.Dispose();
    }
}

[Handler("echo"), AllowAnonymous]
public sealed class EchoHandler : HandlerBase {
    [Action("value")]
    public IResponse Value(JsonElement? value) => Ok(value);
    [Action("empty")]
    public IResponse Empty() => Ok();
    [Action("error")]
    public IResponse Error() => throw new ErrorResponseException<int>("test:rejected", 7);
    [Action("notify")]
    public async Task<IResponse> NotifyAsync() {
        await BroadcastToSelfAsync("changed", new { Value = 11 });
        return Ok();
    }
}

[Handler("private")]
public sealed class PrivateHandler : HandlerBase {
    [Action("value")]
    public IResponse Value() => Ok(123);
}

public sealed class TestAuthenticator : IDarkWsAuthenticator {
    public ValueTask<IDarkWsSession?> AuthenticateAsync(HttpContext context, string? token, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IDarkWsSession?>(token == "valid" ? new TestSession() : null);
    public sealed class TestSession : IDarkWsSession {
        public string Id => "test";
        public ClaimsPrincipal User => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test")], "test"));
        public IReadOnlyCollection<string> Groups => [];
    }
}
