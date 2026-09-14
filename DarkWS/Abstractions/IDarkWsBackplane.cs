namespace DarkWS.Abstractions;

/// <summary>Transport for broadcast delivery across application instances.</summary>
public interface IDarkWsBackplane {
    /// <summary>Publishes one targeted broadcast through the backplane.</summary>
    ValueTask PublishAsync(
        DarkWsBroadcast message,
        CancellationToken cancellationToken = default
    );

    /// <summary>Installs a local listener receiving messages and their cancellation tokens.</summary>
    /// <remarks>The Redis implementation rejects duplicate subscriptions. Its token controls subscription lifetime; unsubscribing cancels active delivery.</remarks>
    ValueTask SubscribeAsync(
        Func<DarkWsBroadcast, CancellationToken, ValueTask> listener,
        CancellationToken cancellationToken = default
    );

    /// <summary>Stops local delivery and releases the active subscription.</summary>
    ValueTask UnsubscribeAsync(CancellationToken cancellationToken = default);
}
