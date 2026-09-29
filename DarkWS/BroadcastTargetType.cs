namespace DarkWS;

/// <summary>Selects a recipient set for a backplane broadcast.</summary>
public enum BroadcastTargetType {
    // Numeric values are part of the Redis wire protocol and must never be reassigned.
    /// <summary>Every connection.</summary>
    All = 0,

    /// <summary>One connection id.</summary>
    Connection = 1,

    /// <summary>Connections in a session.</summary>
    Session = 2,

    /// <summary>Members of a broadcast group.</summary>
    Group = 3,

    /// <summary>The union of several groups, optionally excluding connections or sessions.</summary>
    Groups = 4
}
