namespace DarkWS.Abstractions;

public interface IBroadcaster {
    Task BroadcastAsync(string action, CancellationToken cancellationToken = default);
    Task BroadcastAsync<T>(string action, T? data, CancellationToken cancellationToken = default);
    Task BroadcastToConnectionAsync(string connectionId, string action, CancellationToken cancellationToken = default);
    Task BroadcastToConnectionAsync<T>(string connectionId, string action, T? data, CancellationToken cancellationToken = default);
    Task BroadcastToSessionAsync(string sessionId, string action, CancellationToken cancellationToken = default);
    Task BroadcastToSessionAsync<T>(string sessionId, string action, T? data, CancellationToken cancellationToken = default);
    Task BroadcastToGroupAsync(string group, string action, CancellationToken cancellationToken = default);
    Task BroadcastToGroupAsync<T>(string group, string action, T? data, CancellationToken cancellationToken = default);
}
