using System.Diagnostics;
using System.Net.WebSockets;
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
        IWebSocketConnection connection,
        CancellationToken cancellationToken = default
    ) {
        storage.Add(connection);
        var tasks = new List<Task>();
        var connectionContext = CreateContext(connection, cancellationToken);

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
                    await AuthenticateAsync(message.Data, connection, cancellationToken);
                } else {
                    tasks.RemoveAll(it => it.IsCompleted);
                    tasks.Add(ProcessMessageAsync(message.Data, connection, cancellationToken));
                }
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            logger.LogDebug("WebSocket operation cancelled");
        } catch (WebSocketException error) {
            logger.LogDebug(error, "WebSocket closed");
        } catch (Exception error) {
            logger.LogWarning(error, "WebSocket handler failed");
        } finally {
            await ShutdownAsync(connection, connectionContext, tasks);
        }
    }

    private async Task ProcessMessageAsync(
        byte[] bytes,
        IWebSocketConnection connection,
        CancellationToken cancellationToken
    ) {
        InputMessage? message;
        try {
            message = JsonSerializer.Deserialize<InputMessage>(bytes, _options.JsonOptions);
            if (message is null || string.IsNullOrWhiteSpace(message.Id) || string.IsNullOrWhiteSpace(message.Action)) {
                throw new JsonException("Request id and action are required");
            }
        } catch (JsonException error) {
            logger.LogWarning(error, "Invalid WebSocket request");
            var requestId = TryReadRequestId(bytes);
            if (requestId is not null) {
                await new ErrorResponse(_options.InvalidRequestError)
                    .WriteResultAsync(new ResponseContext(connection, requestId, _options), cancellationToken);
            }
            return;
        }

        var result = await HandleMessageAsync(message, connection, cancellationToken);
        await result.WriteResultAsync(
            new ResponseContext(connection, message.Id, _options),
            cancellationToken
        );
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

        var timer = Stopwatch.StartNew();
        try {
            await using var scope = connection.HttpContext.RequestServices.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<DarkWsContextAccessor>();
            context.Initialize(connection, cancellationToken);

            foreach (var initializer in scope.ServiceProvider.GetServices<IDarkWsScopeInitializer>()) {
                await initializer.InitializeAsync(scope.ServiceProvider, context, cancellationToken);
            }

            var handler = (HandlerBase)scope.ServiceProvider.GetRequiredService(action.HandlerType);
            handler.Initialize(this, context);
            var parameter = action.DeserializeParameter(message.Payload, _options.JsonOptions);
            var result = await action.InvokeAsync(handler, parameter);
            logger.LogInformation("{Action} completed in {Duration}ms", message.Action, timer.ElapsedMilliseconds);
            return result;
        } catch (DarkWsException error) {
            logger.LogDebug(error, "{Action} returned an error in {Duration}ms", message.Action, timer.ElapsedMilliseconds);
            return error.GetResponse();
        } catch (Exception error) {
            logger.LogWarning(error, "{Action} failed in {Duration}ms", message.Action, timer.ElapsedMilliseconds);
            return new ErrorResponse(_options.RequestFailedError);
        }
    }

    private async Task AuthenticateAsync(
        byte[] message,
        IWebSocketConnection connection,
        CancellationToken cancellationToken
    ) {
        var token = Encoding.UTF8.GetString(message.AsSpan(_auth.Length));
        if (string.IsNullOrWhiteSpace(token)) {
            return;
        }

        var session = await authenticator.AuthenticateAsync(connection.HttpContext, token, cancellationToken);
        if (session is null) {
            return;
        }

        var previousSession = connection.Session;
        connection.SetSession(session);
        connection.HttpContext.User = session.User;
        var context = CreateContext(connection, cancellationToken);
        foreach (var middleware in middlewares) {
            await middleware.OnAuthenticatedAsync(context, previousSession);
        }
    }

    private async Task ShutdownAsync(
        IWebSocketConnection connection,
        IDarkWsContextAccessor context,
        IReadOnlyCollection<Task> tasks
    ) {
        try {
            if (tasks.Any(it => !it.IsCompleted)) {
                using var timeout = new CancellationTokenSource(_options.ShutdownTimeout);
                await Task.WhenAll(tasks).WaitAsync(timeout.Token);
            }
        } catch (OperationCanceledException) {
            logger.LogWarning("WebSocket tasks did not finish before shutdown timeout");
        } catch (Exception error) {
            logger.LogWarning(error, "WebSocket task failed during shutdown");
        }

        foreach (var middleware in middlewares) {
            try {
                await middleware.OnCloseAsync(context);
            } catch (Exception error) {
                logger.LogWarning(error, "WebSocket close middleware failed");
            }
        }

        try {
            await connection.CloseAsync(CancellationToken.None);
        } catch (Exception error) {
            logger.LogDebug(error, "WebSocket graceful close failed");
        }

        storage.Remove(connection);
        connection.Dispose();
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
        } catch (JsonException) {
            return null;
        }
    }
}
