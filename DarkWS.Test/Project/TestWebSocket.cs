using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading.Channels;

namespace DarkWS.Test.Project;

public sealed class TestWebSocket : WebSocket {
    private WebSocketState _state = WebSocketState.Open;
    private int _activeSends;
    private int _maxConcurrentSends;
    private readonly Channel<(byte[] Data, bool EndOfMessage, WebSocketCloseStatus? CloseStatus)> _receives = Channel.CreateUnbounded<(byte[], bool, WebSocketCloseStatus?)>();
    private readonly CancellationTokenSource _aborted = new();

    public ConcurrentQueue<byte[]> Sent { get; } = new();
    public ConcurrentQueue<byte[]> SentBuffers { get; } = new();
    public TaskCompletionSource SendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task? SendBarrier { get; set; }
    public Task? CloseBarrier { get; set; }
    public bool IgnoreSendCancellation { get; set; }
    public Exception? SendError { get; set; }
    public bool WasDisposed { get; private set; }
    public TimeSpan SendDelay { get; set; }
    public TimeSpan ReceiveDelay { get; set; }
    public int ReceiveCount { get; private set; }
    public int MaxConcurrentSends => _maxConcurrentSends;
    public WebSocketCloseStatus? LastOutputCloseStatus { get; private set; }
    public bool WasAborted { get; private set; }

    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => _state;
    public override string SubProtocol => string.Empty;

    public override void Abort() {
        WasAborted = true;
        _state = WebSocketState.Aborted;
        _aborted.Cancel();
    }

    public override async Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) {
        if (CloseBarrier is not null) await CloseBarrier.WaitAsync(cancellationToken);
        _state = WebSocketState.Closed;
    }

    public override async Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) {
        if (CloseBarrier is not null) await CloseBarrier.WaitAsync(cancellationToken);
        LastOutputCloseStatus = closeStatus;
        _state = WebSocketState.CloseSent;
    }

    public override void Dispose() {
        WasDisposed = true;
        _aborted.Cancel();
        _state = WebSocketState.Closed;
    }

    public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) {
        using var receiveCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _aborted.Token);
        ReceiveCount++;
        if (ReceiveDelay > TimeSpan.Zero) {
            await Task.Delay(ReceiveDelay, receiveCancellation.Token);
        }
        var receive = await _receives.Reader.ReadAsync(receiveCancellation.Token);
        receive.Data.AsSpan().CopyTo(buffer.AsSpan());
        return new WebSocketReceiveResult(
            receive.Data.Length,
            receive.CloseStatus.HasValue ? WebSocketMessageType.Close : WebSocketMessageType.Text,
            receive.EndOfMessage,
            receive.CloseStatus,
            null
        );
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
            SendStarted.TrySetResult();
            if (SendBarrier is not null) await (IgnoreSendCancellation ? SendBarrier : SendBarrier.WaitAsync(cancellationToken));
            if (SendError is not null) throw SendError;
            if (SendDelay > TimeSpan.Zero) {
                await Task.Delay(SendDelay, cancellationToken);
            }
            Sent.Enqueue(buffer.ToArray());
            SentBuffers.Enqueue(buffer.Array!);
        } finally {
            Interlocked.Decrement(ref _activeSends);
        }
    }

    public void EnqueueReceive(string data, bool endOfMessage = true) {
        _receives.Writer.TryWrite((System.Text.Encoding.UTF8.GetBytes(data), endOfMessage, null));
    }

    public void EnqueueClose() {
        _receives.Writer.TryWrite((Array.Empty<byte>(), true, WebSocketCloseStatus.NormalClosure));
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
