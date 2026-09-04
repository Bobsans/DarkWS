using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace DarkBoy.DarkWS.Test.Project;

public sealed class TestWebSocket : WebSocket {
    private WebSocketState _state = WebSocketState.Open;
    private int _activeSends;
    private int _maxConcurrentSends;
    private readonly Queue<(byte[] Data, bool EndOfMessage, WebSocketCloseStatus? CloseStatus)> _receives = new();

    public ConcurrentQueue<byte[]> Sent { get; } = new();
    public TimeSpan SendDelay { get; set; }
    public int MaxConcurrentSends => _maxConcurrentSends;
    public bool WasAborted { get; private set; }

    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => _state;
    public override string SubProtocol => string.Empty;

    public override void Abort() {
        WasAborted = true;
        _state = WebSocketState.Aborted;
    }

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) {
        _state = WebSocketState.CloseSent;
        return Task.CompletedTask;
    }

    public override void Dispose() {
        _state = WebSocketState.Closed;
    }

    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) {
        var receive = _receives.Dequeue();
        receive.Data.AsSpan().CopyTo(buffer.AsSpan());
        return Task.FromResult(new WebSocketReceiveResult(
            receive.Data.Length,
            WebSocketMessageType.Text,
            receive.EndOfMessage,
            receive.CloseStatus,
            null
        ));
    }

    public override async Task SendAsync(
        ArraySegment<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken
    ) {
        var active = Interlocked.Increment(ref _activeSends);
        InterlockedExtensions.Max(ref _maxConcurrentSends, active);
        try {
            if (SendDelay > TimeSpan.Zero) {
                await Task.Delay(SendDelay, cancellationToken);
            }
            Sent.Enqueue(buffer.ToArray());
        } finally {
            Interlocked.Decrement(ref _activeSends);
        }
    }

    public void EnqueueReceive(string data, bool endOfMessage = true) {
        _receives.Enqueue((System.Text.Encoding.UTF8.GetBytes(data), endOfMessage, null));
    }

    public void SetState(WebSocketState state) {
        _state = state;
    }
}

internal static class InterlockedExtensions {
    public static void Max(ref int target, int value) {
        var current = Volatile.Read(ref target);
        while (current < value) {
            var previous = Interlocked.CompareExchange(ref target, value, current);
            if (previous == current) {
                return;
            }
            current = previous;
        }
    }
}
