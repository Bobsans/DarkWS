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

    // DarkWsTestHost records every publish here, including ones delivered without the backplane.
    internal Action<BroadcastMessage>? Published { get; set; }

    public Task PublishAsync(BroadcastTarget target, string action, CancellationToken cancellationToken) {
        return PublishTargetAsync<object>(target, action, false, null, cancellationToken);
    }

    public Task PublishAsync<T>(BroadcastTarget target, string action, T? data, CancellationToken cancellationToken) {
        return PublishTargetAsync(target, action, true, data, cancellationToken);
    }

    // Overloads with data always send a data field: null becomes JSON null rather than a missing field.
    private Task PublishTargetAsync<T>(BroadcastTarget target, string action, bool hasData, T? data, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        cancellationToken.ThrowIfCancellationRequested();
        if (target.GroupNames is { Count: 0 }) {
            return Task.CompletedTask;
        }

        // A local connection (HandlerBase.Self included) needs no round trip through Redis and every other instance.
        // Its data, often a megabyte-sized sync answer, is serialized once, straight to the bytes the socket sends.
        // Like backplane delivery, the publisher's token does not cancel the write once it has started.
        if (target.Type == BroadcastTargetType.Connection && storage.Find(target.Id!) is { } connection) {
            Published?.Invoke(CreateMessage(target, action, hasData ? ToElement(data) : null));
            object notification = hasData ? new BroadcastActionMessage<T>(action, data) : new BroadcastActionMessage(action);
            return SendAllAsync([connection], JsonSerializer.SerializeToUtf8Bytes(notification, _options.JsonOptions), CancellationToken.None).AsTask();
        }

        var message = CreateMessage(target, action, hasData ? ToElement(data) : null);
        Published?.Invoke(message);
        return backplane.PublishAsync(message, cancellationToken).AsTask();
    }

    // Only unions and exclusions need the Groups envelope; other targets keep the format old Redis nodes read.
    private static BroadcastMessage CreateMessage(BroadcastTarget target, string action, JsonElement? data) {
        return target.Type == BroadcastTargetType.Groups || target.Except is not null
            ? new BroadcastMessage(BroadcastTargetType.Groups, null, action, data) { GroupNames = target.GroupNames ?? [target.Id!], Except = target.Except }
            : new BroadcastMessage(target.Type, target.Id, action, data);
    }

    private JsonElement ToElement<T>(T? data) {
        return JsonSerializer.SerializeToElement(data, _options.JsonOptions);
    }

    internal async ValueTask DeliverAsync(
        BroadcastMessage message,
        CancellationToken cancellationToken
    ) {
        message.ValidateGroupSelection();
        var connections = message.TargetType switch {
            BroadcastTargetType.All => storage.GetAll(),
            BroadcastTargetType.Connection => storage.GetByConnection(RequireTargetId(message)),
            BroadcastTargetType.Session => storage.GetBySession(RequireTargetId(message)),
            BroadcastTargetType.Group => storage.GetByGroup(RequireTargetId(message)),
            BroadcastTargetType.Groups => storage.GetByGroups(message.GroupNames!, message.Except),
            _ => throw new ArgumentOutOfRangeException(nameof(message), message.TargetType, null)
        };
        if (connections.Count == 0) {
            return;
        }

        object notification = message.Data.HasValue
            ? new BroadcastActionMessage<JsonElement>(message.Action, message.Data.Value)
            : new BroadcastActionMessage(message.Action);
        await SendAllAsync(connections, JsonSerializer.SerializeToUtf8Bytes(notification, _options.JsonOptions), cancellationToken);
    }

    private async ValueTask SendAllAsync(IReadOnlyCollection<IWebSocketConnection> connections, byte[] bytes, CancellationToken cancellationToken) {
        // Sends are asynchronous I/O: start every recipient at once so slow sockets cannot delay the rest.
        // They all start now, so one shared deadline equals a per-recipient timeout without a timer per recipient.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.BroadcastSendTimeout);
        await Task.WhenAll(connections.Select(connection => SendAsync(connection, bytes, timeout.Token, cancellationToken)));
    }

    private async Task SendAsync(IWebSocketConnection connection, byte[] bytes, CancellationToken timeout, CancellationToken cancellationToken) {
        if (!connection.IsOpen) {
            return;
        }

        try {
            await connection.SendAsync(bytes, timeout);
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

    private static string RequireTargetId(BroadcastMessage message) {
        return !string.IsNullOrWhiteSpace(message.TargetId)
            ? message.TargetId
            : throw new InvalidOperationException($"Target id is required for {message.TargetType}");
    }
}
