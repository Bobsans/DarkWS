namespace DarkWS;

/// <summary>Selects broadcast recipients. Instances are immutable; Except methods return a copy.</summary>
public sealed class BroadcastTarget {
    private BroadcastTarget(BroadcastTargetType type, string? id, IReadOnlyList<string>? groups, BroadcastExclusion? except) {
        Type = type;
        Id = id;
        GroupNames = groups;
        Except = except;
    }

    /// <summary>Gets every connection.</summary>
    public static BroadcastTarget All { get; } = new(BroadcastTargetType.All, null, null, null);

    /// <summary>Gets the recipient kind.</summary>
    public BroadcastTargetType Type { get; }

    /// <summary>Gets the connection, session, or group key; null for All and Groups.</summary>
    public string? Id { get; }

    /// <summary>Gets the immutable snapshot of distinct groups in first-seen order; null for other kinds.</summary>
    public IReadOnlyList<string>? GroupNames { get; }

    /// <summary>Gets the exclusions of a Group or Groups target, or null.</summary>
    public BroadcastExclusion? Except { get; }

    /// <summary>Selects one connection id.</summary>
    public static BroadcastTarget Connection(string connectionId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        return new BroadcastTarget(BroadcastTargetType.Connection, connectionId, null, null);
    }

    /// <summary>Selects every connection in a session.</summary>
    public static BroadcastTarget Session(string sessionId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return new BroadcastTarget(BroadcastTargetType.Session, sessionId, null, null);
    }

    /// <summary>Selects members of one group.</summary>
    public static BroadcastTarget Group(string group) {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        return new BroadcastTarget(BroadcastTargetType.Group, group, null, null);
    }

    /// <summary>Selects the union of groups, delivering once per connection. The sequence is read once; an empty one publishes nothing.</summary>
    public static BroadcastTarget Groups(IEnumerable<string> groups) {
        ArgumentNullException.ThrowIfNull(groups);
        var ids = groups.Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace)) {
            throw new ArgumentException("Group names must be non-empty.", nameof(groups));
        }
        return new BroadcastTarget(BroadcastTargetType.Groups, null, Array.AsReadOnly(ids), null);
    }

    /// <summary>Returns a copy that skips this connection, replacing an earlier connection exclusion. Group targets only.</summary>
    public BroadcastTarget ExceptConnection(string connectionId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        return WithExcept(new BroadcastExclusion { ConnectionId = connectionId, SessionId = Except?.SessionId });
    }

    /// <summary>Returns a copy that skips every connection of this session, replacing an earlier session exclusion. Group targets only.</summary>
    public BroadcastTarget ExceptSession(string sessionId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return WithExcept(new BroadcastExclusion { ConnectionId = Except?.ConnectionId, SessionId = sessionId });
    }

    // ponytail: the wire carries exclusions only for groups; extend BroadcastMessage validation before allowing other kinds.
    private BroadcastTarget WithExcept(BroadcastExclusion except) {
        return Type is BroadcastTargetType.Group or BroadcastTargetType.Groups
            ? new BroadcastTarget(Type, Id, GroupNames, except)
            : throw new InvalidOperationException("Exclusions are supported only for Group and Groups targets.");
    }
}
