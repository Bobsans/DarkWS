namespace DarkWS.Abstractions;

/// <summary>Publishes notifications through the configured backplane. Recipients share an immutable envelope with id @. Cancellation stops publishing, not delivery that has started.</summary>
public interface IBroadcaster {
    /// <summary>Publishes an action and optional data to all connections through the backplane.</summary>
    Task BroadcastAsync(string action, CancellationToken cancellationToken = default);
    /// <summary>Publishes an action and optional data to all connections through the backplane.</summary>
    Task BroadcastAsync<T>(string action, T? data, CancellationToken cancellationToken = default);
    /// <summary>Publishes an action and optional data to the specified connection id.</summary>
    Task BroadcastToConnectionAsync(string connectionId, string action, CancellationToken cancellationToken = default);
    /// <summary>Publishes an action and optional data to the specified connection id.</summary>
    Task BroadcastToConnectionAsync<T>(string connectionId, string action, T? data, CancellationToken cancellationToken = default);
    /// <summary>Publishes an action and optional data to connections in the specified session.</summary>
    Task BroadcastToSessionAsync(string sessionId, string action, CancellationToken cancellationToken = default);
    /// <summary>Publishes an action and optional data to connections in the specified session.</summary>
    Task BroadcastToSessionAsync<T>(string sessionId, string action, T? data, CancellationToken cancellationToken = default);
    /// <summary>Publishes an action and optional data to members of the specified group.</summary>
    Task BroadcastToGroupAsync(string group, string action, CancellationToken cancellationToken = default);
    /// <summary>Publishes an action and optional data to members of the specified group.</summary>
    Task BroadcastToGroupAsync<T>(string group, string action, T? data, CancellationToken cancellationToken = default);
}
