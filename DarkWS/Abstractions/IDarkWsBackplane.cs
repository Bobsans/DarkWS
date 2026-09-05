namespace DarkWS.Abstractions;

public interface IDarkWsBackplane {
    ValueTask PublishAsync(
        DarkWsBroadcast message,
        CancellationToken cancellationToken = default
    );

    ValueTask SubscribeAsync(
        Func<DarkWsBroadcast, CancellationToken, ValueTask> listener,
        CancellationToken cancellationToken = default
    );

    ValueTask UnsubscribeAsync(CancellationToken cancellationToken = default);
}
