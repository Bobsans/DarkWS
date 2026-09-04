using System.Buffers;
using System.Net.WebSockets;
using DarkBoy.DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;

namespace DarkBoy.DarkWS;

public sealed class WebSocketConnection(
    WebSocket webSocket,
    HttpContext context,
    IDarkWsSession? session
) : IWebSocketConnection {
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public HttpContext HttpContext { get; } = context;
    public WebSocket WebSocket { get; } = webSocket;
    public IDarkWsSession? Session { get; private set; } = session;
    public bool IsOpen => WebSocket.State == WebSocketState.Open;

    void IWebSocketConnection.SetSession(IDarkWsSession? value) {
        Session = value;
    }

    public async Task<ReceivedMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default) {
        using var stream = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        WebSocketReceiveResult result;

        try {
            do {
                result = await WebSocket.ReceiveAsync(buffer, cancellationToken);
                await stream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
            } while (!result.EndOfMessage);
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return new ReceivedMessage(result, stream.ToArray());
    }

    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default) {
        await _sendLock.WaitAsync(cancellationToken);
        try {
            await WebSocket.SendAsync(data, WebSocketMessageType.Text, true, cancellationToken);
        } finally {
            _sendLock.Release();
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default) {
        await _sendLock.WaitAsync(cancellationToken);
        try {
            if (IsOpen) {
                await WebSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Server closing",
                    cancellationToken
                );
            }
        } finally {
            _sendLock.Release();
        }
    }

    public void Dispose() {
        WebSocket.Dispose();
        _sendLock.Dispose();
    }
}
