using System.Buffers;
using System.Net.WebSockets;
using DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;

namespace DarkWS;

/// <summary>Owns a WebSocket with serialized, bounded writes and coordinated disposal. Send buffers are immutable.</summary>
/// <param name="webSocket">Owned WebSocket transport.</param>
/// <param name="context">HTTP upgrade context.</param>
/// <param name="session">Initial session, or null for anonymous access.</param>
/// <param name="maxMessageSizeBytes">Positive complete-message size limit in bytes.</param>
public sealed class WebSocketConnection(
    WebSocket webSocket,
    HttpContext context,
    IDarkWsSession? session,
    int maxMessageSizeBytes
) : IWebSocketConnection {
    private readonly int _maxMessageSizeBytes = maxMessageSizeBytes > 0
        ? maxMessageSizeBytes
        : throw new ArgumentOutOfRangeException(nameof(maxMessageSizeBytes));
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly object _lifecycle = new();
    private readonly CancellationTokenSource _stopping = new();
    private bool _closing;
    private bool _disposed;
    private int _operations;

    /// <summary>Creates a connection with a 1 MiB incoming limit and a 30-second send timeout.</summary>
    public WebSocketConnection(WebSocket webSocket, HttpContext context, IDarkWsSession? session)
        : this(webSocket, context, session, DarkWsOptions.DefaultMaxMessageSizeBytes) { }

    /// <summary>Gets the stable identity for connection or session targeting.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");
    /// <summary>Gets the HTTP upgrade context shared by the connection.</summary>
    public HttpContext HttpContext { get; } = context ?? throw new ArgumentNullException(nameof(context));
    /// <summary>Gets the owned transport. Use connection methods to preserve write serialization and disposal coordination.</summary>
    public WebSocket WebSocket { get; } = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
    /// <summary>Gets the current session, or null for anonymous access.</summary>
    public IDarkWsSession? Session { get; private set; } = session;
    /// <summary>Reports whether the transport is open and has not begun closing.</summary>
    public bool IsOpen => !Volatile.Read(ref _closing) && WebSocket.State == WebSocketState.Open;
    internal TimeSpan ReceiveIdleTimeout { get; init; } = Timeout.InfiniteTimeSpan;
    internal TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(30);

    internal void SetSession(IDarkWsSession? value) {
        Session = value;
    }

    /// <summary>Receives a complete message. Close frames discard partial data; exceeding the size limit closes with status 1009.</summary>
    public async Task<ReceivedMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default) {
        if (!TryBeginOperation()) return new ReceivedMessage(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, WebSocketCloseStatus.NormalClosure, null), []);
        using var stream = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        WebSocketReceiveResult result;
        using var idle = ReceiveIdleTimeout == Timeout.InfiniteTimeSpan
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try {
            do {
                idle?.CancelAfter(ReceiveIdleTimeout);
                result = await WebSocket.ReceiveAsync(buffer, idle?.Token ?? cancellationToken);
                idle?.CancelAfter(Timeout.InfiniteTimeSpan);
                if (result.MessageType == WebSocketMessageType.Close) {
                    return new ReceivedMessage(result, Array.Empty<byte>());
                }
                if (stream.Length + result.Count > _maxMessageSizeBytes) {
                    const string reason = "Message size limit exceeded";
                    using var closeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    closeTimeout.CancelAfter(SendTimeout);
                    try {
                        await _sendLock.WaitAsync(closeTimeout.Token);
                        try {
                            await WebSocket.CloseOutputAsync(WebSocketCloseStatus.MessageTooBig, reason, closeTimeout.Token);
                        } finally {
                            _sendLock.Release();
                        }
                    } catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested) {
                        WebSocket.Abort();
                        throw new TimeoutException("WebSocket close output timed out", error);
                    }
                    return new ReceivedMessage(new WebSocketReceiveResult(
                        0, WebSocketMessageType.Close, true, WebSocketCloseStatus.MessageTooBig, reason
                    ), Array.Empty<byte>());
                }
                await stream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
            } while (!result.EndOfMessage);
        } catch (OperationCanceledException error) when (idle?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested) {
            WebSocket.Abort();
            throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "Receive idle timeout exceeded", error);
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
            EndOperation();
        }

        return new ReceivedMessage(result, stream.ToArray());
    }

    /// <summary>Sends immutable bytes with serialized writes. The built-in transport bounds lock wait and send time and aborts on timeout; closing connections ignore new writes.</summary>
    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(data);
        if (!TryBeginOperation()) return;
        try {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token);
            timeout.CancelAfter(SendTimeout);
            try {
                await _sendLock.WaitAsync(timeout.Token);
                try {
                    if (IsOpen) await WebSocket.SendAsync(data, WebSocketMessageType.Text, true, timeout.Token);
                } finally {
                    _sendLock.Release();
                }
            } catch (OperationCanceledException) when (_stopping.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
                // Shutdown cancels stale broadcasts without reporting an application error.
            } catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested) {
                WebSocket.Abort();
                throw new TimeoutException("WebSocket send timed out", error);
            }
        } finally {
            EndOperation();
        }
    }

    /// <summary>Stops new writes and performs graceful close. Supply cancellation to bound the close handshake.</summary>
    public async Task CloseAsync(CancellationToken cancellationToken = default) {
        BeginClosing();
        if (!TryBeginOperation(allowClosing: true)) return;
        try {
            await _sendLock.WaitAsync(cancellationToken);
            try {
                if (WebSocket.State is WebSocketState.Open or WebSocketState.CloseReceived) {
                    await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closing", cancellationToken);
                }
            } finally {
                _sendLock.Release();
            }
        } finally {
            EndOperation();
        }
    }

    internal void BeginClosing() {
        lock (_lifecycle) {
            if (_closing) return;
            _closing = true;
        }
        // Cancellation callbacks and inline continuations run outside the lock.
        try {
            _stopping.Cancel();
        } catch (ObjectDisposedException) {
            // A concurrent Dispose released the source after the last operation ended; nothing is left to stop.
        }
    }

    /// <summary>Stops operations and releases transport resources after active I/O finishes. Repeated calls are safe.</summary>
    public void Dispose() {
        BeginClosing();
        lock (_lifecycle) {
            if (_disposed) return;
            _disposed = true;
            if (_operations == 0) DisposeResources();
            else WebSocket.Abort();
        }
    }

    private bool TryBeginOperation(bool allowClosing = false) {
        lock (_lifecycle) {
            if (_disposed || _closing && !allowClosing) return false;
            _operations++;
            return true;
        }
    }

    private void EndOperation() {
        lock (_lifecycle) {
            if (--_operations == 0 && _disposed) DisposeResources();
        }
    }

    private void DisposeResources() {
        try {
            WebSocket.Dispose();
        } finally {
            _sendLock.Dispose();
            _stopping.Dispose();
        }
    }
}
