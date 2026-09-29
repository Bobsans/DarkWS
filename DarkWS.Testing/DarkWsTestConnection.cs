using System.Collections.Concurrent;
using System.Security.Claims;
using DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;

namespace DarkWS.Testing;

/// <summary>A transportless connection that captures copies of sent response and broadcast bytes.</summary>
public sealed class DarkWsTestConnection : IWebSocketConnection {
    private readonly ConcurrentQueue<byte[]> _sentMessages = new();

    /// <summary>Creates a connection with a fixed session and optional HTTP context. The session determines the HTTP user.</summary>
    public DarkWsTestConnection(IDarkWsSession? session = null, HttpContext? httpContext = null) {
        Session = session;
        HttpContext = httpContext ?? new DefaultHttpContext();
        HttpContext.User = session?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
    }

    /// <summary>Gets the generated connection id used for targeted broadcasts.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets the supplied or default HTTP context.</summary>
    public HttpContext HttpContext { get; }

    /// <summary>Gets the session supplied when this connection was created.</summary>
    public IDarkWsSession? Session { get; }

    /// <summary>Gets whether the connection accepts sends.</summary>
    public bool IsOpen { get; private set; } = true;

    /// <summary>Gets a snapshot of captured UTF-8 messages, in send order.</summary>
    public IReadOnlyList<byte[]> SentMessages => _sentMessages.ToArray();

    /// <summary>Captures a copy of the data. Closed connections ignore sends, like the server transport.</summary>
    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsOpen) {
            _sentMessages.Enqueue(data.ToArray());
        }
        return Task.CompletedTask;
    }

    /// <summary>Closes the connection without a transport handshake.</summary>
    public Task CloseAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Immediately stops accepting sends.</summary>
    public void Abort() => Dispose();

    /// <summary>Stops accepting sends; captured messages remain available.</summary>
    public void Dispose() => IsOpen = false;
}
