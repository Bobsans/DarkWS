namespace DarkWS.Abstractions;

/// <summary>Local connections of this server instance, indexed by session and group. Lookups return snapshots.</summary>
public interface IDarkWsConnections {
    /// <summary>Returns a snapshot of every local connection.</summary>
    IReadOnlyCollection<IWebSocketConnection> GetAll();

    /// <summary>Returns the local connection with this id, or null.</summary>
    IWebSocketConnection? Find(string connectionId);

    /// <summary>Returns the local connections of a session.</summary>
    IReadOnlyCollection<IWebSocketConnection> GetBySession(string sessionId);

    /// <summary>Returns the local connections whose session belongs to a group.</summary>
    IReadOnlyCollection<IWebSocketConnection> GetByGroup(string group);

    /// <summary>Re-reads the session and groups of a registered connection after the application changed its membership. Returns false for a closed or unregistered connection.</summary>
    bool Refresh(IWebSocketConnection connection);
}
