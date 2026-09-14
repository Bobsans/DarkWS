using DarkWS.Abstractions;

namespace DarkWS;

/// <summary>Thread-safe local registry with session and group indexes. Re-add a connection to refresh externally changed membership.</summary>
public sealed class ConnectionStorage {
    private readonly object _sync = new();
    private readonly Dictionary<string, Entry> _connections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _groups = new(StringComparer.Ordinal);

    /// <summary>Adds or replaces a connection by id and refreshes its session/group indexes.</summary>
    public IWebSocketConnection Add(IWebSocketConnection connection) {
        ArgumentNullException.ThrowIfNull(connection);
        lock (_sync) {
            var entry = new Entry(connection, connection.Session?.Id, connection.Session?.Groups.Distinct(StringComparer.Ordinal).ToArray() ?? []);
            if (_connections.Remove(connection.Id, out var previous)) RemoveIndexes(previous);
            _connections.Add(connection.Id, entry);
            if (entry.SessionId is not null) AddIndex(_sessions, entry.SessionId, connection.Id);
            foreach (var group in entry.Groups) AddIndex(_groups, group, connection.Id);
            return connection;
        }
    }

    /// <summary>Removes this exact connection and its indexes; returns false when absent or replaced.</summary>
    public bool Remove(IWebSocketConnection connection) {
        ArgumentNullException.ThrowIfNull(connection);
        lock (_sync) {
            if (!_connections.TryGetValue(connection.Id, out var entry) || !ReferenceEquals(entry.Connection, connection)) return false;
            _connections.Remove(connection.Id);
            RemoveIndexes(entry);
            return true;
        }
    }

    internal void SetSession(WebSocketConnection connection, IDarkWsSession? session) {
        lock (_sync) {
            connection.SetSession(session);
            if (_connections.TryGetValue(connection.Id, out var entry) && ReferenceEquals(entry.Connection, connection)) Add(connection);
        }
    }

    /// <summary>Returns a snapshot of every local connection.</summary>
    public IReadOnlyCollection<IWebSocketConnection> GetAll() {
        lock (_sync) return _connections.Values.Select(entry => entry.Connection).ToArray();
    }

    /// <summary>Returns the matching connection or an empty snapshot.</summary>
    public IReadOnlyCollection<IWebSocketConnection> GetByConnection(string connectionId) {
        ArgumentNullException.ThrowIfNull(connectionId);
        lock (_sync) return _connections.TryGetValue(connectionId, out var entry) ? [entry.Connection] : [];
    }

    /// <summary>Returns indexed session members; work is proportional to the matching connections.</summary>
    public IReadOnlyCollection<IWebSocketConnection> GetBySession(string sessionId) {
        ArgumentNullException.ThrowIfNull(sessionId);
        lock (_sync) return GetIndexed(_sessions, sessionId);
    }

    /// <summary>Returns indexed group members; work is proportional to the matching connections.</summary>
    public IReadOnlyCollection<IWebSocketConnection> GetByGroup(string group) {
        ArgumentNullException.ThrowIfNull(group);
        lock (_sync) return GetIndexed(_groups, group);
    }

    private IWebSocketConnection[] GetIndexed(Dictionary<string, HashSet<string>> index, string key) {
        return index.TryGetValue(key, out var ids) ? ids.Select(id => _connections[id].Connection).ToArray() : [];
    }

    private static void AddIndex(Dictionary<string, HashSet<string>> index, string key, string id) {
        if (!index.TryGetValue(key, out var ids)) index.Add(key, ids = new HashSet<string>(StringComparer.Ordinal));
        ids.Add(id);
    }

    private void RemoveIndexes(Entry entry) {
        if (entry.SessionId is not null) RemoveIndex(_sessions, entry.SessionId, entry.Connection.Id);
        foreach (var group in entry.Groups) RemoveIndex(_groups, group, entry.Connection.Id);
    }

    private static void RemoveIndex(Dictionary<string, HashSet<string>> index, string key, string id) {
        var ids = index[key];
        ids.Remove(id);
        if (ids.Count == 0) index.Remove(key);
    }

    private sealed record Entry(IWebSocketConnection Connection, string? SessionId, string[] Groups);
}
