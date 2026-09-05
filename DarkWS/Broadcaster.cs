using System.Net.WebSockets;
using System.Text.Json;
using DarkWS.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DarkWS;

internal sealed class Broadcaster(
    IDarkWsBackplane backplane,
    ConnectionStorage storage,
    IOptions<DarkWsOptions> options,
    ILogger<Broadcaster> logger
) : IBroadcaster {
    private readonly DarkWsOptions _options = options.Value;

    public Task BroadcastAsync(string action, CancellationToken cancellationToken = default) {
        return PublishAsync(DarkWsTarget.All, null, action, null, cancellationToken);
    }

    public Task BroadcastAsync<T>(string action, T? data, CancellationToken cancellationToken = default) {
        return PublishAsync(DarkWsTarget.All, null, action, ToElement(data), cancellationToken);
    }

    public Task BroadcastToConnectionAsync(string connectionId, string action, CancellationToken cancellationToken = default) {
        return PublishAsync(DarkWsTarget.Connection, connectionId, action, null, cancellationToken);
    }

    public Task BroadcastToConnectionAsync<T>(string connectionId, string action, T? data, CancellationToken cancellationToken = default) {
        return PublishAsync(DarkWsTarget.Connection, connectionId, action, ToElement(data), cancellationToken);
    }

    public Task BroadcastToSessionAsync(string sessionId, string action, CancellationToken cancellationToken = default) {
        return PublishAsync(DarkWsTarget.Session, sessionId, action, null, cancellationToken);
    }

    public Task BroadcastToSessionAsync<T>(string sessionId, string action, T? data, CancellationToken cancellationToken = default) {
        return PublishAsync(DarkWsTarget.Session, sessionId, action, ToElement(data), cancellationToken);
    }

    public Task BroadcastToGroupAsync(string group, string action, CancellationToken cancellationToken = default) {
        return PublishAsync(DarkWsTarget.Group, group, action, null, cancellationToken);
    }

    public Task BroadcastToGroupAsync<T>(string group, string action, T? data, CancellationToken cancellationToken = default) {
        return PublishAsync(DarkWsTarget.Group, group, action, ToElement(data), cancellationToken);
    }

    private Task PublishAsync(
        DarkWsTarget target,
        string? targetId,
        string action,
        JsonElement? data,
        CancellationToken cancellationToken
    ) {
        return backplane.PublishAsync(
            new DarkWsBroadcast(target, targetId, action, data),
            cancellationToken
        ).AsTask();
    }

    private JsonElement? ToElement<T>(T? data) {
        return data is null ? null : JsonSerializer.SerializeToElement(data, _options.JsonOptions);
    }

    internal async ValueTask DeliverAsync(
        DarkWsBroadcast message,
        CancellationToken cancellationToken
    ) {
        var connections = message.Target switch {
            DarkWsTarget.All => storage.GetAll(),
            DarkWsTarget.Connection => storage.GetByConnection(RequireTargetId(message)),
            DarkWsTarget.Session => storage.GetBySession(RequireTargetId(message)),
            DarkWsTarget.Group => storage.GetByGroup(RequireTargetId(message)),
            _ => throw new ArgumentOutOfRangeException(nameof(message), message.Target, null)
        };

        object data = message.Data.HasValue
            ? new BroadcastActionMessage<JsonElement>(message.Action, message.Data.Value)
            : new BroadcastActionMessage(message.Action);

        await Parallel.ForEachAsync(connections, cancellationToken, async (connection, token) => {
            if (connection.WebSocket.State != WebSocketState.Open) {
                return;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(_options.BroadcastSendTimeout);
            try {
                await new ResponseContext(connection, "@", _options)
                    .SendAsync(new ResponseMessage<object>("@", data), timeout.Token);
            } catch (OperationCanceledException) when (!token.IsCancellationRequested) {
                logger.LogWarning("Broadcast to {ConnectionId} timed out; aborting connection", connection.Id);
                connection.WebSocket.Abort();
            } catch (Exception error) {
                logger.LogWarning(error, "Cannot broadcast to {ConnectionId}", connection.Id);
            }
        });
    }

    private static string RequireTargetId(DarkWsBroadcast message) {
        return !string.IsNullOrWhiteSpace(message.TargetId)
            ? message.TargetId
            : throw new InvalidOperationException($"Target id is required for {message.Target}");
    }
}
