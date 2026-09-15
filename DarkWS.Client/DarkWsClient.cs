using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace DarkWS.Client;

/// <summary>A concurrent, lazy DarkWS client. One instance owns one server session.</summary>
public sealed partial class DarkWsClient : IDarkWsClient {
    private readonly object _sync = new();
    private readonly DarkWsClientOptions _options;
    private readonly SemaphoreSlim _authentication = new(1, 1);
    private readonly List<Subscription> _subscriptions = [];
    private readonly Channel<Notification> _notifications;
    private readonly Channel<Action> _events = Channel.CreateUnbounded<Action>(new() { SingleReader = true });
    private readonly CancellationTokenSource _disposed = new();
    private Task? _notificationTask;
    private Task? _eventTask;
    private Cycle? _cycle;
    private Task _stopping = Task.CompletedTask;
    private DarkWsClientState _state;
    private bool _closed;
    private bool _automaticAuthentication = true;
    private long _authenticationVersion;
    private int _pendingCount;

    /// <summary>Creates a client using default settings. No connection is opened.</summary>
    public DarkWsClient(Uri endpoint) : this(new DarkWsClientOptions { Endpoint = endpoint }) { }

    /// <summary>Validates and snapshots settings. No connection is opened.</summary>
    public DarkWsClient(DarkWsClientOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Snapshot();
        _notifications = Channel.CreateBounded<Notification>(new BoundedChannelOptions(_options.NotificationQueueCapacity) {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false
        });
    }

    /// <inheritdoc />
    public DarkWsClientState State { get { lock (_sync) return _state; } }
    /// <inheritdoc />
    public event EventHandler<DarkWsStateChangedEventArgs>? StateChanged;
    /// <inheritdoc />
    public event EventHandler<DarkWsClientErrorEventArgs>? Error;

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default) =>
        _ = await GetConnectionAsync(true, cancellationToken).ConfigureAwait(false);

    private async Task<Connection> GetConnectionAsync(bool explicitly, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        Task<Connection> ready;
        lock (_sync) {
            ThrowIfDisposed();
            if (_cycle is null) {
                if (_closed && !explicitly) throw Closed();
                _closed = false;
                var cycle = new Cycle(_disposed.Token);
                _cycle = cycle;
                ChangeState(DarkWsClientState.Connecting);
                var previousStop = _stopping;
                cycle.Run = Task.Run(() => RunCycleAsync(cycle, previousStop));
            }
            ready = _cycle.Ready.Task;
        }
        try { return await ready.WaitAsync(_options.ConnectionTimeout, cancellationToken).ConfigureAwait(false); }
        catch (TimeoutException) { throw new DarkWsTimeoutException(DarkWsTimeoutStage.Connection); }
    }

    private async Task RunCycleAsync(Cycle cycle, Task previousStop) {
        var attempt = 0;
        try {
            await previousStop.ConfigureAwait(false);
            while (!cycle.Stop.IsCancellationRequested && !cycle.Stopping) {
                using var connection = new Connection(cycle.Stop.Token);
                lock (_sync) {
                    if (_cycle != cycle || cycle.Stopping) return;
                    cycle.Connection = connection;
                }
                Exception failure;
                var permanent = false;
                var establishing = true;
                Task receive = Task.CompletedTask;
                Task heartbeat = Task.CompletedTask;
                try {
                    using var attemptTimeout = CancellationTokenSource.CreateLinkedTokenSource(connection.Token);
                    attemptTimeout.CancelAfter(_options.ConnectionTimeout);
                    var token = attemptTimeout.Token;
                    if (_options.ConfigureWebSocketOptionsAsync is { } configure) {
                        try { await configure(connection.Socket.Options, token).AsTask().WaitAsync(token).ConfigureAwait(false); }
                        catch (Exception) when (!token.IsCancellationRequested) { permanent = true; throw new DarkWsConnectionException("Socket configuration failed."); }
                    }
                    connection.Socket.Options.CollectHttpResponseDetails = true;
                    await connection.Socket.ConnectAsync(_options.Endpoint, token).ConfigureAwait(false);
                    receive = ReceiveAsync(connection);
                    if (_options.AuthenticationTokenProvider is { } provider && AutomaticAuthenticationEnabled()) {
                        permanent = true;
                        string? authenticationToken;
                        try { authenticationToken = await provider(token).AsTask().WaitAsync(token).ConfigureAwait(false); }
                        catch (Exception) when (!token.IsCancellationRequested) { throw new DarkWsConnectionException("Authentication token provider failed."); }
                        if (string.IsNullOrWhiteSpace(authenticationToken))
                            throw new DarkWsConnectionException("Authentication token provider returned no credentials.");
                        if (AutomaticAuthenticationEnabled())
                            await ExchangeControlAsync(connection, authenticationToken, token, connecting: true).ConfigureAwait(false);
                        permanent = false;
                    }
                    lock (_sync) {
                        if (_cycle != cycle || cycle.Stopping) return;
                        connection.Token.ThrowIfCancellationRequested();
                        ChangeState(DarkWsClientState.Connected);
                        cycle.Ready.TrySetResult(connection);
                    }
                    establishing = false;
                    attempt = 0;
                    heartbeat = HeartbeatAsync(connection);
                    await await Task.WhenAny(receive, heartbeat).ConfigureAwait(false);
                    throw Closed(connection.Socket);
                } catch (Exception exception) {
                    permanent |= exception is DarkWsProtocolException or DarkWsClientLimitException or DarkWsResponseException ||
                        connection.Socket.HttpStatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
                    failure = connection.Failure ?? (exception is OperationCanceledException && !connection.Token.IsCancellationRequested && establishing
                        ? new DarkWsTimeoutException(DarkWsTimeoutStage.Connection)
                        : SafeFailure(exception));
                    connection.FailRequests(failure);
                    if (exception is DarkWsProtocolException protocol)
                        await CloseOutputAsync(connection, protocol.CloseStatus).ConfigureAwait(false);
                } finally {
                    connection.Stop();
                    while (_notifications.Reader.TryRead(out _)) { }
                    try { await Task.WhenAll(receive, heartbeat).ConfigureAwait(false); } catch { /* Observed by the connection cycle. */ }
                }

                connection.FailRequests(failure);
                lock (_sync) {
                    if (_cycle != cycle || cycle.Stopping) return;
                    cycle.Connection = null;
                    if (permanent || !_options.Reconnect) {
                        cycle.Ready.TrySetException(failure);
                        _cycle = null;
                        _closed = true;
                        ChangeState(DarkWsClientState.Disconnected, failure);
                        ReportError(failure);
                        return;
                    }
                    if (cycle.Ready.Task.IsCompleted) cycle.Ready = NewCompletion<Connection>();
                    ChangeState(DarkWsClientState.Reconnecting, failure);
                    ReportError(failure);
                }
                var delay = Math.Min(30_000, 1000 * Math.Pow(2, Math.Min(attempt++, 5)));
                await Task.Delay(TimeSpan.FromMilliseconds(delay * (0.5 + Random.Shared.NextDouble() * 0.5)), cycle.Stop.Token).ConfigureAwait(false);
            }
        } catch (OperationCanceledException) when (cycle.Stop.IsCancellationRequested) {
            // Closing a cycle cancels delays and transport operations together.
        } finally {
            cycle.Ready.TrySetException(Closed());
            cycle.Stop.Dispose();
        }
    }

    private bool AutomaticAuthenticationEnabled() { lock (_sync) return _automaticAuthentication; }

    /// <inheritdoc />
#pragma warning disable RS0026 // Initial API: distinguish omitted payload from explicit null.
    public Task<TResponse> RequestAsync<TResponse>(string action, CancellationToken cancellationToken = default) =>
        RequestTypedAsync<TResponse>(action, null, false, cancellationToken);
    /// <inheritdoc />
    public Task<TResponse> RequestAsync<TResponse>(string action, object? payload, CancellationToken cancellationToken = default) =>
        RequestTypedAsync<TResponse>(action, payload, true, cancellationToken);
    /// <inheritdoc />
    public async Task RequestAsync(string action, CancellationToken cancellationToken = default) =>
        _ = await RequestCoreAsync(action, null, false, cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    public async Task RequestAsync(string action, object? payload, CancellationToken cancellationToken = default) =>
        _ = await RequestCoreAsync(action, payload, true, cancellationToken).ConfigureAwait(false);
#pragma warning restore RS0026

    private async Task<T> RequestTypedAsync<T>(string action, object? payload, bool hasPayload, CancellationToken cancellationToken) {
        var data = await RequestCoreAsync(action, payload, hasPayload, cancellationToken).ConfigureAwait(false);
        if (!data.HasValue) throw new DarkWsProtocolException("The typed response has no data field.");
        return data.Value.Deserialize<T>(_options.JsonOptions)!;
    }

    private async Task<JsonElement?> RequestCoreAsync(string action, object? payload, bool hasPayload, CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        lock (_sync) ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Increment(ref _pendingCount) > _options.MaxPendingRequests) {
            Interlocked.Decrement(ref _pendingCount);
            throw new DarkWsClientLimitException("The pending request limit was reached.");
        }
        try {
            var connection = await GetConnectionAsync(false, cancellationToken).ConfigureAwait(false);
            return await ExchangeAsync(connection, action, payload, hasPayload, cancellationToken).ConfigureAwait(false);
        } finally { Interlocked.Decrement(ref _pendingCount); }
    }

    private async Task<JsonElement?> ExchangeAsync(Connection connection, string action, object? payload, bool hasPayload, CancellationToken cancellationToken) {
        var id = Guid.NewGuid().ToString("N");
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) {
            writer.WriteStartObject();
            writer.WriteString("id", id);
            writer.WriteString("action", action);
            if (hasPayload) {
                writer.WritePropertyName("data");
                JsonSerializer.Serialize(writer, payload, _options.JsonOptions);
            }
            writer.WriteEndObject();
        }
        var pending = new Pending(action);
        connection.Pending.TryAdd(id, pending);
        try {
            var send = SendAsync(connection, stream.ToArray(), cancellationToken);
            Observe(send);
            // Caller cancellation stops waiting, never a write already in progress on the shared socket.
            await send.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { return await pending.Completion.Task.WaitAsync(_options.RequestTimeout, cancellationToken).ConfigureAwait(false); }
            catch (TimeoutException exception) when (exception is not DarkWsTimeoutException) {
                throw new DarkWsTimeoutException(DarkWsTimeoutStage.Response);
            }
        } finally { connection.Pending.TryRemove(id, out _); }
    }

    private async Task SendAsync(Connection connection, byte[] bytes, CancellationToken callerToken) {
        using var timeout = new CancellationTokenSource(_options.SendTimeout);
        using var queue = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, connection.Token, callerToken);
        try { await connection.SendGate.WaitAsync(queue.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) {
            callerToken.ThrowIfCancellationRequested();
            if (connection.Token.IsCancellationRequested) throw connection.Failure ?? Closed();
            throw new DarkWsTimeoutException(DarkWsTimeoutStage.Send);
        }
        try {
            callerToken.ThrowIfCancellationRequested();
            if (connection.Token.IsCancellationRequested) throw connection.Failure ?? Closed();
            // Publish the write failure before aborting the socket; otherwise the reader can
            // observe the abort first and incorrectly classify it as a connection timeout.
            using var deadline = timeout.Token.Register(() => connection.Fail(new DarkWsTimeoutException(DarkWsTimeoutStage.Send)));
            try { await connection.Socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, connection.Token).ConfigureAwait(false); }
            catch (Exception exception) {
                var failure = connection.Failure ?? SafeFailure(exception);
                connection.Fail(failure);
                throw failure;
            }
        } finally { connection.SendGate.Release(); }
    }

    /// <inheritdoc />
    public Task AuthenticateAsync(string token, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return ChangeAuthenticationAsync(token, cancellationToken);
    }

    /// <inheritdoc />
    public Task LogoutAsync(CancellationToken cancellationToken = default) {
        lock (_sync) {
            ThrowIfDisposed();
            _automaticAuthentication = false;
            _authenticationVersion++;
        }
        return ChangeAuthenticationAsync(null, cancellationToken);
    }

    private async Task ChangeAuthenticationAsync(string? token, CancellationToken cancellationToken) {
        lock (_sync) ThrowIfDisposed();
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposed.Token);
        if (Interlocked.Increment(ref _pendingCount) > _options.MaxPendingRequests) {
            Interlocked.Decrement(ref _pendingCount);
            throw new DarkWsClientLimitException("The pending request limit was reached.");
        }
        var entered = false;
        try {
            await _authentication.WaitAsync(wait.Token).ConfigureAwait(false);
            entered = true;
            long version;
            lock (_sync) version = _authenticationVersion;
            var connection = await GetConnectionAsync(false, wait.Token).ConfigureAwait(false);
            await ExchangeControlAsync(connection, token, wait.Token).ConfigureAwait(false);
            if (token is not null) {
                lock (_sync) {
                    if (version == _authenticationVersion) _automaticAuthentication = true;
                }
            }
        } finally {
            if (entered) _authentication.Release();
            Interlocked.Decrement(ref _pendingCount);
        }
    }

    private async Task ExchangeControlAsync(Connection connection, string? token, CancellationToken cancellationToken, bool connecting = false) {
        var pending = new Pending(token is null ? "logout" : "auth");
        Volatile.Write(ref connection.Control, pending);
        try {
            var bytes = System.Text.Encoding.UTF8.GetBytes(token is null ? "logout" : "auth:" + token);
            var send = SendAsync(connection, bytes, cancellationToken);
            Observe(send);
            await send.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { await pending.Completion.Task.WaitAsync(_options.RequestTimeout, cancellationToken).ConfigureAwait(false); }
            catch (TimeoutException exception) when (exception is not DarkWsTimeoutException) {
                throw new DarkWsTimeoutException(DarkWsTimeoutStage.Response);
            }
        } catch (Exception exception) when (exception is not DarkWsResponseException) {
            // System replies have no id. Discard this socket rather than reuse an ambiguous acknowledgement.
            connection.Fail(connecting && exception is OperationCanceledException && !connection.Token.IsCancellationRequested
                ? new DarkWsTimeoutException(DarkWsTimeoutStage.Connection) : SafeFailure(exception));
            throw;
        } finally { Interlocked.CompareExchange(ref connection.Control, null, pending); }
    }

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken cancellationToken = default) {
        Task stopping;
        lock (_sync) {
            ThrowIfDisposed();
            _closed = true;
            if (_cycle is { } cycle) {
                _cycle = null;
                cycle.Stopping = true;
                cycle.Ready.TrySetException(Closed());
                cycle.Connection?.FailRequests(Closed());
                _stopping = Task.Run(() => StopCycleAsync(cycle));
            }
            ChangeState(DarkWsClientState.Disconnected);
            stopping = _stopping;
        }
        await stopping.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task StopCycleAsync(Cycle cycle) {
        if (cycle.Connection is { } connection)
            await CloseOutputAsync(connection, WebSocketCloseStatus.NormalClosure).ConfigureAwait(false);
        try { cycle.Stop.Cancel(); } catch (ObjectDisposedException) { /* Cycle already finished. */ }
        cycle.Connection?.Stop();
        await cycle.Run.ConfigureAwait(false);
    }

    private async Task CloseOutputAsync(Connection connection, WebSocketCloseStatus status) {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(connection.Token);
        timeout.CancelAfter(_options.CloseTimeout);
        var entered = false;
        try {
            await connection.SendGate.WaitAsync(timeout.Token).ConfigureAwait(false);
            entered = true;
            if (connection.Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await connection.Socket.CloseOutputAsync(status, null, timeout.Token).ConfigureAwait(false);
            if (status == WebSocketCloseStatus.NormalClosure)
                await connection.PeerClosed.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        } catch (Exception exception) when (exception is OperationCanceledException or WebSocketException or ObjectDisposedException) {
            // Graceful close is bounded; the cycle aborts the remaining transport work.
        } finally { if (entered) connection.SendGate.Release(); }
    }

    /// <summary>Immediately stops network activity. Prefer DisposeAsync when awaiting network cleanup is possible.</summary>
    public void Dispose() {
        lock (_sync) {
            if (_state == DarkWsClientState.Disposed) return;
            _closed = true;
            if (_cycle is { } cycle) {
                _cycle = null;
                cycle.Stopping = true;
                cycle.Ready.TrySetException(new ObjectDisposedException(nameof(DarkWsClient)));
                cycle.Stop.Cancel();
                cycle.Connection?.Fail(new ObjectDisposedException(nameof(DarkWsClient)));
                _stopping = Task.WhenAll(_stopping, cycle.Run);
            }
            _disposed.Cancel();
            foreach (var subscription in _subscriptions.ToArray()) subscription.Dispose();
            _subscriptions.Clear();
            _notifications.Writer.TryComplete();
            ChangeState(DarkWsClientState.Disposed);
            _events.Writer.TryComplete();
        }
    }

    /// <summary>Stops and awaits internal network tasks. Does not wait indefinitely for application callbacks.</summary>
    public async ValueTask DisposeAsync() {
        Dispose();
        await _stopping.ConfigureAwait(false);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_state == DarkWsClientState.Disposed, this);
    private static DarkWsConnectionException Closed(ClientWebSocket? socket = null) =>
        new("The DarkWS connection is closed.", socket?.CloseStatus, socket?.CloseStatusDescription);
    private static Exception SafeFailure(Exception exception) => exception is DarkWsConnectionException or DarkWsTimeoutException or DarkWsProtocolException or DarkWsClientLimitException or DarkWsResponseException
        ? exception : new DarkWsConnectionException("The WebSocket transport failed.");
    private static TaskCompletionSource<T> NewCompletion<T>() {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Observe(completion.Task);
        return completion;
    }
    private static void Observe(Task task) => _ = task.ContinueWith(t => _ = t.Exception,
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private sealed class Cycle(CancellationToken disposalToken) {
        internal readonly CancellationTokenSource Stop = CancellationTokenSource.CreateLinkedTokenSource(disposalToken);
        internal TaskCompletionSource<Connection> Ready = NewCompletion<Connection>();
        internal Task Run = Task.CompletedTask;
        internal Connection? Connection;
        internal volatile bool Stopping;
    }

    private sealed class Pending(string action) {
        internal readonly string Action = action;
        internal readonly TaskCompletionSource<JsonElement?> Completion = NewCompletion<JsonElement?>();
    }

    private sealed class Connection : IDisposable {
        internal readonly ClientWebSocket Socket = new();
        internal readonly SemaphoreSlim SendGate = new(1, 1);
        internal readonly ConcurrentDictionary<string, Pending> Pending = new();
        internal readonly TaskCompletionSource<bool> PeerClosed = NewCompletion<bool>();
        internal TaskCompletionSource<bool>? Pong;
        internal Pending? Control;
        private readonly CancellationTokenSource _stop;
        internal readonly CancellationToken Token;
        private Exception? _failure;
        internal Exception? Failure => Volatile.Read(ref _failure);
        internal Connection(CancellationToken cycleToken) {
            _stop = CancellationTokenSource.CreateLinkedTokenSource(cycleToken);
            Token = _stop.Token;
        }
        internal void FailRequests(Exception exception) {
            foreach (var pending in Pending.Values) pending.Completion.TrySetException(exception);
            Volatile.Read(ref Control)?.Completion.TrySetException(exception);
        }
        internal void Fail(Exception exception) {
            Interlocked.CompareExchange(ref _failure, exception, null);
            FailRequests(Failure!);
            Stop();
        }
        internal void Stop() {
            try { _stop.Cancel(); } catch (ObjectDisposedException) { /* A concurrent close already released this connection. */ }
            Socket.Abort();
        }
        public void Dispose() {
            FailRequests(Failure ?? Closed());
            Stop();
            Socket.Dispose();
            _stop.Dispose();
            // SendGate may still be held by a write whose caller stopped waiting.
        }
    }
}
