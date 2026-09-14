using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DarkWS.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DarkWS;

internal sealed class WebSocketHandler(
    DarkWsActionRegistry actions,
    ConnectionStorage storage,
    IBroadcaster broadcaster,
    IDarkWsAuthenticator authenticator,
    IEnumerable<DarkWsMiddleware> middlewares,
    IOptions<DarkWsOptions> options,
    ILogger<WebSocketHandler> logger
) {
    private static readonly byte[] _ping = "ping"u8.ToArray();
    private static readonly byte[] _pong = "pong"u8.ToArray();
    private static readonly byte[] _auth = "auth:"u8.ToArray();
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

        try {
            foreach (var middleware in middlewares) {
                await middleware.OnOpenAsync(connectionContext);
            }

            while (connection.IsOpen && !cancellationToken.IsCancellationRequested) {
                var message = await connection.ReceiveMessageAsync(cancellationToken);
                if (message.CloseStatus.HasValue) {
                    break;
                }

                if (message.Data.AsSpan().SequenceEqual(_ping)) {
                    await connection.SendAsync(_pong, cancellationToken);
                } else if (message.Data.AsSpan().StartsWith(_auth)) {
                    var result = await AuthenticateAsync(Encoding.UTF8.GetString(message.Data.AsSpan(_auth.Length)), connection, cancellationToken);
                    await result.WriteResultAsync(new ResponseContext(connection, DarkWsProtocol.LegacyAuthenticationId, _options), cancellationToken);
                } else {
                    var input = await ReadMessageAsync(message.Data, connection, cancellationToken);
                    if (input is null) continue;
                    if (input.Action is "darkws:authenticate" or "darkws:logout") {
                        await ProcessControlAsync(input, connection, cancellationToken);
                        continue;
                    }
                    tasks.RemoveAll(it => it.IsCompleted);
                    if (tasks.Count >= _options.MaxConcurrentRequestsPerConnection) {
                        await Task.WhenAny(tasks).WaitAsync(cancellationToken);
                        tasks.RemoveAll(it => it.IsCompleted);
                    }
                    tasks.Add(ProcessMessageAsync(input, connection, requests.Token));
                }
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            Log.Cancelled(logger, null);
        } catch (WebSocketException error) {
            Log.Closed(logger, error);
        } catch (Exception error) {
            Log.HandlerFailed(logger, error);
        } finally {
            await ShutdownAsync(connection, connectionContext, tasks, requests);
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
        try {
            var result = await HandleMessageAsync(message, connection, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await result.WriteResultAsync(new ResponseContext(connection, message.Id, _options), cancellationToken);
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
        CancellationToken cancellationToken
    ) {
        if (!actions.TryGet(message.Action, out var action)) {
            return new ErrorResponse(_options.InvalidActionError);
        }

        if (!action.AllowAnonymous && connection.Session?.User.Identity?.IsAuthenticated != true) {
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
            await using var scope = connection.HttpContext.RequestServices.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<DarkWsContextAccessor>();
            context.Initialize(connection, cancellationToken);

            foreach (var initializer in scope.ServiceProvider.GetServices<IDarkWsScopeInitializer>()) {
                await initializer.InitializeAsync(scope.ServiceProvider, context, cancellationToken);
            }

            var handler = (HandlerBase)scope.ServiceProvider.GetRequiredService(action.HandlerType);
            handler.Initialize(this, context);
            var result = await action.InvokeAsync(handler, parameter);
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

    private async Task<IResponse> AuthenticateAsync(
        string? token,
        WebSocketConnection connection,
        CancellationToken cancellationToken
    ) {
        IDarkWsSession? session = null;
        try {
            if (!string.IsNullOrWhiteSpace(token)) session = await authenticator.AuthenticateAsync(connection.HttpContext, token, cancellationToken);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception error) {
            Log.AuthenticationFailed(logger, error);
        }

        await SetSessionAsync(connection, session, cancellationToken);
        return session is null ? new ErrorResponse(_options.AuthenticationFailedError) : new SuccessResponse();
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

    private async Task ProcessControlAsync(InputMessage message, WebSocketConnection connection, CancellationToken cancellationToken) {
        IResponse result;
        if (message.Action == "darkws:logout") {
            await SetSessionAsync(connection, null, cancellationToken);
            result = new SuccessResponse();
        } else {
            result = await AuthenticateAsync(message.Payload is { ValueKind: JsonValueKind.String } payload ? payload.GetString() : null, connection, cancellationToken);
        }
        await result.WriteResultAsync(new ResponseContext(connection, message.Id, _options), cancellationToken);
    }

    private async Task ShutdownAsync(
        WebSocketConnection connection,
        IDarkWsContextAccessor context,
        List<Task> tasks,
        CancellationTokenSource requests
    ) {
        storage.Remove(connection);
        connection.BeginClosing();
        using var timeout = new CancellationTokenSource(_options.ShutdownTimeout);
        tasks.Add(requests.CancelAsync());
        try {
            await Task.WhenAll(tasks).WaitAsync(timeout.Token);
        } catch (OperationCanceledException) {
            Log.ShutdownTimeout(logger, null);
        } catch (Exception error) {
            Log.ShutdownFailed(logger, error);
        }

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
        context.Initialize(connection, cancellationToken);
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
