using DarkWS.Abstractions;

namespace DarkWS;

internal sealed class InMemoryDarkWsBackplane : IDarkWsBackplane {
    private Func<DarkWsBroadcast, CancellationToken, ValueTask>? _listener;

    public ValueTask PublishAsync(
        DarkWsBroadcast message,
        CancellationToken cancellationToken = default
    ) {
        if (cancellationToken.IsCancellationRequested) return ValueTask.FromCanceled(cancellationToken);
        var listener = Volatile.Read(ref _listener);
        // The publisher's token must not cancel writes to other connections once delivery has started.
        return listener is null ? ValueTask.CompletedTask : listener(message, CancellationToken.None);
    }

    public ValueTask SubscribeAsync(
        Func<DarkWsBroadcast, CancellationToken, ValueTask> listener,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(listener);
        Interlocked.Exchange(ref _listener, listener);
        return ValueTask.CompletedTask;
    }

    public ValueTask UnsubscribeAsync(CancellationToken cancellationToken = default) {
        Interlocked.Exchange(ref _listener, null);
        return ValueTask.CompletedTask;
    }
}
