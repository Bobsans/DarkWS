using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DarkWS.Client;

public sealed partial class DarkWsClient {
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private async Task ReceiveAsync(Connection connection) {
        try { await ReadMessagesAsync(connection).ConfigureAwait(false); }
        catch (Exception exception) when (!connection.Token.IsCancellationRequested) {
            // No reply can arrive any more: fail waiters now, not at their timeouts. A dropped socket also cancels
            // connect-time authentication. Wire and capacity errors keep the token so the cycle still classifies
            // them as terminal and sends the close status.
            var failure = connection.Failure ?? SafeFailure(exception);
            if (failure is DarkWsProtocolException or DarkWsClientLimitException) connection.FailRequests(failure);
            else connection.Fail(failure);
            throw;
        }
    }

    private async Task ReadMessagesAsync(Connection connection) {
        var buffer = new byte[Math.Min(8192, _options.MaxMessageSizeBytes)];
        while (!connection.Token.IsCancellationRequested) {
            using var message = new MemoryStream();
            ValueWebSocketReceiveResult result;
            do {
                try { result = await connection.Socket.ReceiveAsync(buffer.AsMemory(), connection.Token).ConfigureAwait(false); }
                catch (WebSocketException exception) when (exception.WebSocketErrorCode == WebSocketError.Faulted) {
                    // ManagedWebSocket validates text and frame structure before returning bytes.
                    // It has already sent the corresponding protocol close frame.
                    throw new DarkWsProtocolException("The WebSocket transport rejected an invalid message.");
                }
                if (result.MessageType == WebSocketMessageType.Close) {
                    connection.PeerClosed.TrySetResult(true);
                    throw Closed(connection.Socket);
                }
                if (result.MessageType != WebSocketMessageType.Text)
                    throw new DarkWsProtocolException("Binary messages are not supported.", WebSocketCloseStatus.InvalidMessageType);
                if (message.Length + result.Count > _options.MaxMessageSizeBytes)
                    throw new DarkWsProtocolException("The incoming message size limit was exceeded.", WebSocketCloseStatus.MessageTooBig);
                message.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            string text;
            try { text = StrictUtf8.GetString(message.GetBuffer(), 0, (int)message.Length); }
            catch (DecoderFallbackException) { throw new DarkWsProtocolException("The message is not valid UTF-8.", WebSocketCloseStatus.InvalidPayloadData); }
            if (text == "pong") {
                Volatile.Read(ref connection.Pong)?.TrySetResult(true);
                continue;
            }
            if (text is "auth:success" or "auth:failed" or "logout:success") {
                var control = Volatile.Read(ref connection.Control);
                if (control is not null && text.StartsWith(control.Action + ":", StringComparison.Ordinal)) {
                    if (text == "auth:failed")
                        control.Completion.TrySetException(new DarkWsResponseException(text, null, string.Empty, control.Action));
                    else control.Completion.TrySetResult(null);
                }
                continue;
            }
            try {
                using var document = JsonDocument.Parse(text);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String)
                    throw new DarkWsProtocolException("The response must contain a string id.");
                var id = idElement.GetString()!;
                if (id == "@auth") continue;
                if (root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.String)
                    throw new DarkWsProtocolException("The response error must be a string.");
                JsonElement? data = root.TryGetProperty("data", out var dataElement) ? dataElement.Clone() : null;
                if (id == "@") {
                    if (error.ValueKind != JsonValueKind.Undefined ||
                        !root.TryGetProperty("action", out var action) || action.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(action.GetString()))
                        throw new DarkWsProtocolException("The broadcast must contain an action.");
                    EnqueueNotification(connection, action.GetString()!, data);
                } else if (connection.Pending.TryRemove(id, out var pending)) {
                    if (error.ValueKind == JsonValueKind.String)
                        pending.Completion.TrySetException(new DarkWsResponseException(error.GetString()!, data, id, pending.Action));
                    else pending.Completion.TrySetResult(data);
                }
            } catch (JsonException) { throw new DarkWsProtocolException("The message is not valid JSON."); }
        }
    }

    private async Task HeartbeatAsync(Connection connection) {
        try {
            while (true) {
                await Task.Delay(_options.PingInterval, connection.Token).ConfigureAwait(false);
                var pong = NewCompletion<bool>();
                Volatile.Write(ref connection.Pong, pong);
                await SendAsync(connection, "ping"u8.ToArray(), connection.Token).ConfigureAwait(false);
                try { await pong.Task.WaitAsync(_options.PongTimeout, connection.Token).ConfigureAwait(false); }
                catch (TimeoutException) { throw new DarkWsConnectionException("The server did not acknowledge the heartbeat."); }
                Volatile.Write(ref connection.Pong, null);
            }
        } catch (Exception exception) {
            if (!connection.Token.IsCancellationRequested) connection.Fail(SafeFailure(exception));
            throw;
        }
    }

    /// <inheritdoc />
    public IDisposable On(string action, Action handler) {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe(action, (_, _) => { handler(); return Task.CompletedTask; });
    }

    /// <inheritdoc />
    public IDisposable On<T>(string action, Action<T> handler) {
        ArgumentNullException.ThrowIfNull(handler);
        return OnAsync<T>(action, (data, _) => { handler(data); return Task.CompletedTask; });
    }

    /// <inheritdoc />
    public IDisposable OnAsync<T>(string action, Func<T, CancellationToken, Task> handler) {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe(action, (data, token) => {
            if (!data.HasValue) throw new JsonException("The typed notification has no data field.");
            return handler(data.Value.Deserialize<T>(_options.JsonOptions)!, token);
        });
    }

    private IDisposable Subscribe(string action, Func<JsonElement?, CancellationToken, Task> handler) {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        lock (_sync) {
            ThrowIfDisposed();
            var subscription = new Subscription(this, action, handler);
            _subscriptions.Add(subscription);
            return subscription;
        }
    }

    private void EnqueueNotification(Connection connection, string action, JsonElement? data) {
        lock (_sync) {
            if (_state == DarkWsClientState.Disposed || connection.Token.IsCancellationRequested) return;
            var subscriptions = _subscriptions.Where(subscription => subscription.Action == action).ToArray();
            if (subscriptions.Length == 0) return;
            if (!_notifications.Writer.TryWrite(new Notification(connection, data, subscriptions)))
                throw new DarkWsClientLimitException("The notification queue is full; reconnect explicitly after refreshing application state.");
            _notificationTask ??= Task.Run(DispatchNotificationsAsync);
        }
    }

    private async Task DispatchNotificationsAsync() {
        await foreach (var notification in _notifications.Reader.ReadAllAsync().ConfigureAwait(false)) {
            var token = notification.Connection.Token;
            foreach (var subscription in notification.Subscriptions) {
                if (token.IsCancellationRequested || _disposed.IsCancellationRequested) break;
                try {
                    // Acquire the invocation before leaving the subscription lock; Dispose prevents future acquisitions.
                    var callback = subscription.Acquire();
                    if (callback is not null) {
                        var invocation = callback(notification.Data, token);
                        Observe(invocation);
                        await invocation.WaitAsync(token).ConfigureAwait(false);
                    }
                } catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception exception) { ReportError(exception); }
            }
        }
    }

    // Called under _sync so queued transitions preserve lifecycle order.
    private void ChangeState(DarkWsClientState state, Exception? reason = null) {
        if (_state == state) return;
        var change = new DarkWsStateChangedEventArgs(_state, state, reason);
        _state = state;
        var handlers = StateChanged;
        if (handlers is not null) QueueEvent(() => {
            foreach (EventHandler<DarkWsStateChangedEventArgs> handler in handlers.GetInvocationList()) {
                try { handler(this, change); }
                catch (Exception exception) { ReportError(exception); }
            }
        });
    }

    private void ReportError(Exception exception) {
        var handlers = Error;
        if (handlers is null) return;
        QueueEvent(() => {
            foreach (EventHandler<DarkWsClientErrorEventArgs> handler in handlers.GetInvocationList()) {
                try { handler(this, new DarkWsClientErrorEventArgs(exception)); }
                catch { /* Error observers cannot recursively report their own failures. */ }
            }
        });
    }

    private void QueueEvent(Action callback) {
        lock (_sync) {
            if (_events.Writer.TryWrite(callback)) _eventTask ??= Task.Run(async () => {
                await foreach (var action in _events.Reader.ReadAllAsync().ConfigureAwait(false)) action();
            });
        }
    }

    private sealed record Notification(Connection Connection, JsonElement? Data, Subscription[] Subscriptions);

    private sealed class Subscription(DarkWsClient owner, string action, Func<JsonElement?, CancellationToken, Task> handler) : IDisposable {
        internal readonly string Action = action;
        private Func<JsonElement?, CancellationToken, Task>? _handler = handler;
        internal Func<JsonElement?, CancellationToken, Task>? Acquire() {
            lock (owner._sync) return _handler;
        }
        public void Dispose() {
            lock (owner._sync) {
                _handler = null;
                owner._subscriptions.Remove(this);
            }
        }
    }
}
