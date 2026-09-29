namespace DarkWS.Abstractions;

/// <summary>Publishes notifications through the configured backplane. Recipients share an immutable envelope with id @. Cancellation stops publishing, not delivery that has started.</summary>
public interface IBroadcaster {
    /// <summary>Publishes an action without data to the selected recipients.</summary>
    Task PublishAsync(BroadcastTarget target, string action) =>
        PublishAsync(target, action, CancellationToken.None);

    /// <summary>Publishes an action without data to the selected recipients.</summary>
    Task PublishAsync(BroadcastTarget target, string action, CancellationToken cancellationToken);

    /// <summary>Publishes an action and data, including explicit null, to the selected recipients.</summary>
    Task PublishAsync<T>(BroadcastTarget target, string action, T? data) =>
        PublishAsync(target, action, data, CancellationToken.None);

    /// <summary>Publishes an action and data, including explicit null, to the selected recipients.</summary>
    Task PublishAsync<T>(BroadcastTarget target, string action, T? data, CancellationToken cancellationToken);
}
