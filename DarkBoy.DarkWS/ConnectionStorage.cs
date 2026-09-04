using System.Collections.Concurrent;
using DarkBoy.DarkWS.Abstractions;

namespace DarkBoy.DarkWS;

public sealed class ConnectionStorage {
    private readonly ConcurrentDictionary<string, IWebSocketConnection> _connections = new();

    public IWebSocketConnection Add(IWebSocketConnection connection) {
        _connections[connection.Id] = connection;
        return connection;
    }

    public bool Remove(IWebSocketConnection connection) {
        return _connections.TryRemove(connection.Id, out _);
    }

    public IReadOnlyCollection<IWebSocketConnection> GetAll() {
        return _connections.Values.ToArray();
    }

    public IReadOnlyCollection<IWebSocketConnection> GetByConnection(string connectionId) {
        return _connections.TryGetValue(connectionId, out var connection) ? [connection] : [];
    }

    public IReadOnlyCollection<IWebSocketConnection> GetBySession(string sessionId) {
        return _connections.Values.Where(it => it.Session?.Id == sessionId).ToArray();
    }

    public IReadOnlyCollection<IWebSocketConnection> GetByGroup(string group) {
        return _connections.Values
            .Where(it => it.Session?.Groups.Contains(group, StringComparer.Ordinal) == true)
            .ToArray();
    }
}
