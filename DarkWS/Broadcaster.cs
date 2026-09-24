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

    // Overloads with data always send a data field: null becomes JSON null rather than a missing field.
    private JsonElement? ToElement<T>(T? data) {
        return JsonSerializer.SerializeToElement(data, _options.JsonOptions);
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

        object notification = message.Data.HasValue
            ? new BroadcastActionMessage<JsonElement>(message.Action, message.Data.Value)
            : new BroadcastActionMessage(message.Action);
        if (connections.Count == 0) return;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(notification, _options.JsonOptions);

        // Sends are asynchronous I/O: start every recipient at once so slow sockets cannot delay the rest.
        await Task.WhenAll(connections.Select(connection => SendAsync(connection, bytes, cancellationToken)));
    }

    private async Task SendAsync(IWebSocketConnection connection, byte[] bytes, CancellationToken cancellationToken) {
        if (!connection.IsOpen) {
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.BroadcastSendTimeout);
        try {
            await connection.SendAsync(bytes, timeout.Token);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            logger.LogWarning("Broadcast to {ConnectionId} timed out; aborting connection", connection.Id);
            try {
                connection.Abort();
            } catch (Exception error) {
                logger.LogWarning(error, "Cannot abort {ConnectionId} after a broadcast timeout", connection.Id);
            }
        } catch (Exception error) when (error is ObjectDisposedException or WebSocketException || error is OperationCanceledException && cancellationToken.IsCancellationRequested) {
            logger.LogDebug(error, "Broadcast connection {ConnectionId} closed or was cancelled", connection.Id);
        } catch (Exception error) {
            logger.LogWarning(error, "Cannot broadcast to {ConnectionId}", connection.Id);
        }
    }

    private static string RequireTargetId(DarkWsBroadcast message) {
        return !string.IsNullOrWhiteSpace(message.TargetId)
            ? message.TargetId
            : throw new InvalidOperationException($"Target id is required for {message.Target}");
    }
}
