using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using DarkWS.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DarkWS;

internal sealed class WebSocketHandler(
    DarkWsActionRegistry actions,
    ConnectionStorage storage,
    IBroadcaster broadcaster,
    IEnumerable<DarkWsMiddleware> middlewares,
    IOptions<DarkWsOptions> options,
    ILogger<WebSocketHandler> logger
) {
    private static readonly byte[] _ping = "ping"u8.ToArray();
    private static readonly byte[] _pong = "pong"u8.ToArray();
    private static readonly byte[] _auth = "auth:"u8.ToArray();
    private static readonly byte[] _authSuccess = "auth:success"u8.ToArray();
    private static readonly byte[] _authFailed = "auth:failed"u8.ToArray();
    private static readonly byte[] _logout = "logout"u8.ToArray();
    private static readonly byte[] _logoutSuccess = "logout:success"u8.ToArray();
    private readonly DarkWsOptions _options = options.Value;

    public IBroadcaster Broadcaster { get; } = broadcaster;

    public async Task AcceptAsync(
        WebSocketConnection connection,
        CancellationToken cancellationToken = default
    ) {
        storage.Add(connection);
        var tasks = new List<Task>();
        var requests = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var connectionContext = CreateContext(connection, requests.Token);
        var queue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(_options.MaxConcurrentRequestsPerConnection) {
            SingleReader = true,
            SingleWriter = true
        });
        Task? reader = null;

        try {
            foreach (var middleware in middlewares) {
                await middleware.OnOpenAsync(connectionContext);
            }

            reader = ReceiveAsync(connection, queue.Writer, requests, cancellationToken);
            try {
                // Messages are handled in arrival order; the reader cancels requests when it stops.
                while (await queue.Reader.WaitToReadAsync(requests.Token) && queue.Reader.TryPeek(out var data)) {
                    if (data.AsSpan().StartsWith(_auth)) {
                        queue.Reader.TryRead(out _);
                        var authenticated = await AuthenticateAsync(Encoding.UTF8.GetString(data.AsSpan(_auth.Length)), connection, cancellationToken);
                        await connection.SendAsync(authenticated ? _authSuccess : _authFailed, cancellationToken);
                    } else if (data.AsSpan().SequenceEqual(_logout)) {
                        queue.Reader.TryRead(out _);
                        await SetSessionAsync(connection, null, cancellationToken);
                        await connection.SendAsync(_logoutSuccess, cancellationToken);
                    } else {
                        var input = await ReadMessageAsync(data, connection, cancellationToken);
                        if (input is not null) {
                            tasks.RemoveAll(it => it.IsCompleted);
                            // The request keeps its queue place while waiting for a slot, so the queue bound stays exact.
                            if (tasks.Count >= _options.MaxConcurrentRequestsPerConnection) {
                                await Task.WhenAny(tasks).WaitAsync(requests.Token);
                                tasks.RemoveAll(it => it.IsCompleted);
                            }
                            tasks.Add(ProcessMessageAsync(input, connection, requests.Token));
                        }
                        queue.Reader.TryRead(out _);
                    }
                }
            } catch (OperationCanceledException) when (requests.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
                // The reader stopped: the peer closed or the transport failed. Its outcome is observed below.
            }
            await reader;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            Log.Cancelled(logger, null);
        } catch (WebSocketException error) {
            Log.Closed(logger, error);
        } catch (Exception error) {
            Log.HandlerFailed(logger, error);
        } finally {
            await ShutdownAsync(connection, tasks, requests, reader);
        }
    }

    // Keeps a receive pending even while every request slot is busy, so keep-alive PONGs and ping are
    // processed under load. Everything else is queued for the dispatcher in arrival order.
    private async Task ReceiveAsync(
        WebSocketConnection connection,
        ChannelWriter<byte[]> queue,
        CancellationTokenSource requests,
        CancellationToken cancellationToken
    ) {
        try {
            while (connection.IsOpen && !cancellationToken.IsCancellationRequested) {
                var message = await connection.ReceiveMessageAsync(cancellationToken);
                if (message.CloseStatus.HasValue) {
                    return;
                }

                if (message.Data.AsSpan().SequenceEqual(_ping)) {
                    await connection.SendAsync(_pong, cancellationToken);
                } else if (!queue.TryWrite(message.Data)) {
                    await EnqueueAsync(message.Data, connection, queue, requests.Token);
                }
            }
        } finally {
            // Like the former single loop, a stopped reader ends dispatching and cancels running handlers.
            requests.Cancel();
        }
    }

    private async Task EnqueueAsync(byte[] data, IWebSocketConnection connection, ChannelWriter<byte[]> queue, CancellationToken cancellationToken) {
        if (data.AsSpan().StartsWith(_auth) || data.AsSpan().SequenceEqual(_logout)) {
            // Commands are rare and must keep their order, so they wait for a place.
            await queue.WriteAsync(data, cancellationToken);
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.RequestQueueTimeout);
        try {
            await queue.WriteAsync(data, timeout.Token);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            var input = await ReadMessageAsync(data, connection, cancellationToken);
            if (input is not null) {
                await new ErrorResponse(_options.BusyError).WriteResultAsync(new ResponseContext(connection, input.Id, _options), cancellationToken);
            }
        }
    }

    private async Task<InputMessage?> ReadMessageAsync(
        byte[] bytes,
        IWebSocketConnection connection,
        CancellationToken cancellationToken
    ) {
        InputMessage? message;
        try {
            message = JsonSerializer.Deserialize<InputMessage>(bytes, _options.JsonOptions);
            if (message is null || string.IsNullOrWhiteSpace(message.Id) || DarkWsProtocol.IsReservedRequestId(message.Id) || string.IsNullOrWhiteSpace(message.Action)) {
                throw new JsonException("Request id and action are required");
            }
            return message;
        } catch (JsonException error) {
            Log.InvalidRequest(logger, error);
            var requestId = TryReadRequestId(bytes);
            if (requestId is not null) {
                await new ErrorResponse(_options.InvalidRequestError)
                    .WriteResultAsync(new ResponseContext(connection, DarkWsProtocol.IsReservedRequestId(requestId) ? string.Empty : requestId, _options), cancellationToken);
            }
            return null;
        }
    }

    private async Task ProcessMessageAsync(InputMessage message, IWebSocketConnection connection, CancellationToken cancellationToken) {
        var response = new ResponseContext(connection, message.Id, _options);
        try {
            try {
                // The result is written inside the message scope, so deferred data can still use scoped services.
                await using var scope = connection.HttpContext.RequestServices.CreateAsyncScope();
                var result = await HandleMessageAsync(message, connection, scope.ServiceProvider, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                await result.WriteResultAsync(response, cancellationToken);
            } catch (Exception error) when (!response.HasStarted && !cancellationToken.IsCancellationRequested) {
                // Nothing was sent yet, so the stable error cannot duplicate or follow a partial result.
                Log.ResponseFailed(logger, message.Id, error);
                await new ErrorResponse(_options.RequestFailedError).WriteResultAsync(response, cancellationToken);
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            Log.RequestCancelled(logger, message.Id, null);
        } catch (WebSocketException error) {
            Log.ResponseClosed(logger, message.Id, error);
        } catch (Exception error) {
            Log.ResponseFailed(logger, message.Id, error);
        }
    }

    private async Task<IResponse> HandleMessageAsync(
        InputMessage message,
        IWebSocketConnection connection,
        IServiceProvider services,
        CancellationToken cancellationToken
    ) {
        if (!actions.TryGet(message.Action, out var action)) {
            return new ErrorResponse(_options.InvalidActionError);
        }

        // The action keeps the session it was authorized with, even if auth:/logout arrives meanwhile.
        var session = connection.Session;
        if (!action.AllowAnonymous && session?.User.Identity?.IsAuthenticated != true) {
            return new ErrorResponse(_options.AuthorizationRequiredError);
        }

        var started = Stopwatch.GetTimestamp();
        try {
            object? parameter;
            try {
                parameter = action.DeserializeParameter(message.Payload, _options.JsonOptions);
            } catch (Exception error) when (error is JsonException or NotSupportedException) {
                Log.InvalidPayload(logger, message.Action, error);
                return new ErrorResponse(_options.InvalidRequestError);
            }
            var context = services.GetRequiredService<DarkWsContextAccessor>();
            context.Initialize(connection, cancellationToken, session);

            foreach (var initializer in services.GetServices<IDarkWsScopeInitializer>()) {
                await initializer.InitializeAsync(services, context, cancellationToken);
            }

            var handler = (HandlerBase)services.GetRequiredService(action.HandlerType);
            handler.Initialize(this, context);
            var result = await action.InvokeAsync(handler, parameter)
                ?? throw new InvalidOperationException($"Action {message.Action} returned no response");
            if (logger.IsEnabled(LogLevel.Debug)) Log.Completed(logger, message.Action, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, null);
            return result;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (DarkWsException error) {
            if (logger.IsEnabled(LogLevel.Debug)) Log.ControlledError(logger, message.Action, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, error);
            return error.GetResponse();
        } catch (Exception error) {
            if (logger.IsEnabled(LogLevel.Warning)) Log.ActionFailed(logger, message.Action, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, error);
            return new ErrorResponse(_options.RequestFailedError);
        }
    }

    private async Task<bool> AuthenticateAsync(
        string? token,
        WebSocketConnection connection,
        CancellationToken cancellationToken
    ) {
        IDarkWsSession? session = null;
        try {
            if (!string.IsNullOrWhiteSpace(token)) {
                // A fresh scope per auth: command, so scoped dependencies such as a DbContext do not live for the connection.
                await using var scope = connection.HttpContext.RequestServices.CreateAsyncScope();
                session = await scope.ServiceProvider.GetRequiredService<IDarkWsAuthenticator>()
                    .AuthenticateAsync(connection.HttpContext, token, cancellationToken);
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception error) {
            Log.AuthenticationFailed(logger, error);
        }

        await SetSessionAsync(connection, session, cancellationToken);
        return session is not null;
    }

    private async Task SetSessionAsync(WebSocketConnection connection, IDarkWsSession? session, CancellationToken cancellationToken) {
        var previousSession = connection.Session;
        storage.SetSession(connection, session);
        connection.HttpContext.User = session?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        var context = CreateContext(connection, cancellationToken);
        foreach (var middleware in middlewares) {
            await middleware.OnAuthenticatedAsync(context, previousSession);
        }
    }

    private async Task ShutdownAsync(
        WebSocketConnection connection,
        List<Task> tasks,
        CancellationTokenSource requests,
        Task? reader
    ) {
        // Close first, so a ConnectionStorage.Add racing with removal sees a closed connection and ignores it.
        connection.BeginClosing();
        storage.Remove(connection);
        using var timeout = new CancellationTokenSource(_options.ShutdownTimeout);
        tasks.Add(requests.CancelAsync());
        try {
            await Task.WhenAll(tasks).WaitAsync(timeout.Token);
        } catch (OperationCanceledException) {
            Log.ShutdownTimeout(logger, null);
        } catch (Exception error) {
            Log.ShutdownFailed(logger, error);
        }

        // The request token is already cancelled; close hooks get the shutdown deadline instead.
        var context = CreateContext(connection, timeout.Token);
        foreach (var middleware in middlewares) {
            try {
                var closing = middleware.OnCloseAsync(context);
                tasks.Add(closing);
                await closing.WaitAsync(timeout.Token);
            } catch (Exception error) {
                Log.CloseMiddlewareFailed(logger, error);
            }
        }

        try {
            var close = connection.CloseAsync(timeout.Token);
            tasks.Add(close);
            await close.WaitAsync(timeout.Token);
        } catch (Exception error) {
            Log.CloseFailed(logger, error);
            connection.WebSocket.Abort();
        }

        if (reader is not null) {
            // A reader still waiting for data after the close attempt would keep the connection undisposed.
            if (!reader.IsCompleted) connection.WebSocket.Abort();
            tasks.Add(reader);
        }
        _ = DisposeWhenCompletedAsync(Task.WhenAll(tasks), connection, requests);
    }

    private async Task DisposeWhenCompletedAsync(Task tasks, WebSocketConnection connection, CancellationTokenSource requests) {
        try {
            await tasks;
        } catch (Exception error) {
            Log.CleanupFailed(logger, error);
        } finally {
            try {
                connection.Dispose();
            } catch (Exception error) {
                Log.DisposalFailed(logger, error);
            } finally {
                requests.Dispose();
            }
        }
    }

    private static DarkWsContextAccessor CreateContext(
        IWebSocketConnection connection,
        CancellationToken cancellationToken
    ) {
        var context = new DarkWsContextAccessor();
        context.Initialize(connection, cancellationToken, connection.Session);
        return context;
    }

    private static string? TryReadRequestId(byte[] bytes) {
        try {
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        } catch (Exception error) when (error is JsonException or InvalidOperationException) {
            return null;
        }
    }

    private static class Log {
        public static readonly Action<ILogger, Exception?> Cancelled =
            LoggerMessage.Define(LogLevel.Debug, new EventId(1, nameof(Cancelled)), "WebSocket operation cancelled");
        public static readonly Action<ILogger, Exception?> Closed =
            LoggerMessage.Define(LogLevel.Debug, new EventId(2, nameof(Closed)), "WebSocket closed");
        public static readonly Action<ILogger, Exception?> HandlerFailed =
            LoggerMessage.Define(LogLevel.Warning, new EventId(3, nameof(HandlerFailed)), "WebSocket handler failed");
        public static readonly Action<ILogger, Exception?> InvalidRequest =
            LoggerMessage.Define(LogLevel.Debug, new EventId(4, nameof(InvalidRequest)), "Invalid WebSocket request");
        public static readonly Action<ILogger, string, Exception?> RequestCancelled =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(5, nameof(RequestCancelled)), "Request {RequestId} cancelled during connection shutdown");
        public static readonly Action<ILogger, string, Exception?> ResponseClosed =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(6, nameof(ResponseClosed)), "Connection closed while responding to {RequestId}");
        public static readonly Action<ILogger, string, Exception?> ResponseFailed =
            LoggerMessage.Define<string>(LogLevel.Warning, new EventId(7, nameof(ResponseFailed)), "Cannot process or send response for {RequestId}");
        public static readonly Action<ILogger, string, Exception?> InvalidPayload =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(8, nameof(InvalidPayload)), "Invalid payload for {Action}");
        public static readonly Action<ILogger, string, long, Exception?> Completed =
            LoggerMessage.Define<string, long>(LogLevel.Debug, new EventId(9, nameof(Completed)), "{Action} completed in {Duration}ms");
        public static readonly Action<ILogger, string, long, Exception?> ControlledError =
            LoggerMessage.Define<string, long>(LogLevel.Debug, new EventId(10, nameof(ControlledError)), "{Action} returned an error in {Duration}ms");
        public static readonly Action<ILogger, string, long, Exception?> ActionFailed =
            LoggerMessage.Define<string, long>(LogLevel.Warning, new EventId(11, nameof(ActionFailed)), "{Action} failed in {Duration}ms");
        public static readonly Action<ILogger, Exception?> AuthenticationFailed =
            LoggerMessage.Define(LogLevel.Warning, new EventId(12, nameof(AuthenticationFailed)), "Re-authentication failed");
        public static readonly Action<ILogger, Exception?> ShutdownTimeout =
            LoggerMessage.Define(LogLevel.Warning, new EventId(13, nameof(ShutdownTimeout)), "WebSocket tasks did not finish before shutdown timeout");
        public static readonly Action<ILogger, Exception?> ShutdownFailed =
            LoggerMessage.Define(LogLevel.Warning, new EventId(14, nameof(ShutdownFailed)), "WebSocket task failed during shutdown");
        public static readonly Action<ILogger, Exception?> CloseMiddlewareFailed =
            LoggerMessage.Define(LogLevel.Warning, new EventId(15, nameof(CloseMiddlewareFailed)), "WebSocket close middleware failed");
        public static readonly Action<ILogger, Exception?> CloseFailed =
            LoggerMessage.Define(LogLevel.Debug, new EventId(16, nameof(CloseFailed)), "WebSocket graceful close failed");
        public static readonly Action<ILogger, Exception?> CleanupFailed =
            LoggerMessage.Define(LogLevel.Debug, new EventId(17, nameof(CleanupFailed)), "Connection cleanup observed a completed task failure");
        public static readonly Action<ILogger, Exception?> DisposalFailed =
            LoggerMessage.Define(LogLevel.Warning, new EventId(18, nameof(DisposalFailed)), "Connection resource disposal failed");
    }
}
