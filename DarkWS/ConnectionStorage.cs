using DarkWS.Abstractions;

namespace DarkWS;

/// <summary>Thread-safe local registry with session and group indexes.</summary>
internal sealed class ConnectionStorage : IDarkWsConnections {
    private readonly object _sync = new();
    private readonly Dictionary<string, Entry> _connections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _groups = new(StringComparer.Ordinal);

    /// <summary>Adds or replaces an open connection by id and refreshes its session/group indexes. A closed connection is ignored.</summary>
    public IWebSocketConnection Add(IWebSocketConnection connection) {
        Index(connection, registeredOnly: false);
        return connection;
    }

    public bool Refresh(IWebSocketConnection connection) => Index(connection, registeredOnly: true);

    private bool Index(IWebSocketConnection connection, bool registeredOnly) {
        ArgumentNullException.ThrowIfNull(connection);
        while (true) {
            if (!connection.IsOpen) {
                return false;
            }

            var session = connection.Session;
            var entry = new Entry(connection, session?.Id, session?.Groups.Distinct(StringComparer.Ordinal).ToArray() ?? []);
            lock (_sync) {
                // A refresh racing with shutdown must not re-register a connection that was already removed.
                if (!connection.IsOpen) {
                    return false;
                }
                if (registeredOnly && !(_connections.TryGetValue(connection.Id, out var current) && ReferenceEquals(current.Connection, connection))) {
                    return false;
                }
                // Re-authentication may have replaced the session while its groups were being read.
                if (!ReferenceEquals(session, connection.Session)) {
                    continue;
                }

                AddEntry(entry);
                return true;
            }
        }
    }

    /// <summary>Removes this exact connection and its indexes; returns false when absent or replaced.</summary>
    public bool Remove(IWebSocketConnection connection) {
        ArgumentNullException.ThrowIfNull(connection);
        lock (_sync) {
            if (!_connections.TryGetValue(connection.Id, out var entry) || !ReferenceEquals(entry.Connection, connection)) {
                return false;
            }

            _connections.Remove(connection.Id);
            RemoveIndexes(entry);
            return true;
        }
    }

    internal void SetSession(WebSocketConnection connection, IDarkWsSession? session) {
        var snapshot = new Entry(connection, session?.Id, session?.Groups.Distinct(StringComparer.Ordinal).ToArray() ?? []);
        lock (_sync) {
            connection.SetSession(session);
            if (connection.IsOpen && _connections.TryGetValue(connection.Id, out var entry) && ReferenceEquals(entry.Connection, connection)) {
                AddEntry(snapshot);
            }
        }
    }

    private void AddEntry(Entry entry) {
        var id = entry.Connection.Id;
        if (_connections.Remove(id, out var previous)) {
            RemoveIndexes(previous);
        }

        _connections.Add(id, entry);
        if (entry.SessionId is not null) {
            AddIndex(_sessions, entry.SessionId, id);
        }

        foreach (var group in entry.Groups) {
            AddIndex(_groups, group, id);
        }
    }

    /// <summary>Returns a snapshot of every local connection.</summary>
    public IReadOnlyCollection<IWebSocketConnection> GetAll() {
        lock (_sync) {
            return _connections.Values.Select(entry => entry.Connection).ToArray();
        }
    }

    /// <summary>Returns the matching connection or an empty snapshot.</summary>
    public IReadOnlyCollection<IWebSocketConnection> GetByConnection(string connectionId) {
        return Find(connectionId) is { } connection ? [connection] : [];
    }

    public IWebSocketConnection? Find(string connectionId) {
        ArgumentNullException.ThrowIfNull(connectionId);
        lock (_sync) {
            return _connections.TryGetValue(connectionId, out var entry) ? entry.Connection : null;
        }
    }

    /// <summary>Returns indexed session members; work is proportional to the matching connections.</summary>
    public IReadOnlyCollection<IWebSocketConnection> GetBySession(string sessionId) {
        ArgumentNullException.ThrowIfNull(sessionId);
        lock (_sync) {
            return GetIndexed(_sessions, sessionId);
        }
    }

    /// <summary>Returns indexed group members; work is proportional to the matching connections.</summary>
    public IReadOnlyCollection<IWebSocketConnection> GetByGroup(string group) {
        ArgumentNullException.ThrowIfNull(group);
        lock (_sync) {
            return GetIndexed(_groups, group);
        }
    }

    private IWebSocketConnection[] GetIndexed(Dictionary<string, HashSet<string>> index, string key) {
        return index.TryGetValue(key, out var ids) ? ids.Select(id => _connections[id].Connection).ToArray() : [];
    }

    // Select and exclude from one indexed snapshot, including the session used to index each connection.
    internal IReadOnlyCollection<IWebSocketConnection> GetByGroups(IReadOnlyList<string> groups, BroadcastExclusion? except) {
        lock (_sync) {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in groups) {
                if (_groups.TryGetValue(group, out var members)) {
                    ids.UnionWith(members);
                }
            }
            if (except?.ConnectionId is { } connectionId) {
                ids.Remove(connectionId);
            }
            if (except?.SessionId is { } sessionId && _sessions.TryGetValue(sessionId, out var sessionMembers)) {
                ids.ExceptWith(sessionMembers);
            }
            return ids.Select(id => _connections[id].Connection).ToArray();
        }
    }

    private static void AddIndex(Dictionary<string, HashSet<string>> index, string key, string id) {
        if (!index.TryGetValue(key, out var ids)) {
            index.Add(key, ids = new HashSet<string>(StringComparer.Ordinal));
        }

        ids.Add(id);
    }

    private void RemoveIndexes(Entry entry) {
        if (entry.SessionId is not null) {
            RemoveIndex(_sessions, entry.SessionId, entry.Connection.Id);
        }

        foreach (var group in entry.Groups) {
            RemoveIndex(_groups, group, entry.Connection.Id);
        }
    }

    private static void RemoveIndex(Dictionary<string, HashSet<string>> index, string key, string id) {
        var ids = index[key];
        ids.Remove(id);
        if (ids.Count == 0) {
            index.Remove(key);
        }
    }

    private sealed record Entry(IWebSocketConnection Connection, string? SessionId, string[] Groups);
}
