using DarkWS.Abstractions;

namespace DarkWS;

internal sealed class InMemoryDarkWsBackplane : IDarkWsBackplane {
    private Func<BroadcastMessage, CancellationToken, ValueTask>? _listener;

    public ValueTask PublishAsync(
        BroadcastMessage message,
        CancellationToken cancellationToken = default
    ) {
        if (cancellationToken.IsCancellationRequested) {
            return ValueTask.FromCanceled(cancellationToken);
        }

        var listener = Volatile.Read(ref _listener);
        // The publisher's token must not cancel writes to other connections once delivery has started.
        return listener?.Invoke(message, CancellationToken.None) ?? ValueTask.CompletedTask;
    }

    public ValueTask SubscribeAsync(
        Func<BroadcastMessage, CancellationToken, ValueTask> listener,
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
