using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkWS;

/// <summary>Selects a recipient set for a backplane broadcast.</summary>
public enum DarkWsTarget {
    // Numeric values are part of the Redis wire protocol and must never be reassigned.
    /// <summary>Every connection.</summary>
    All = 0,
    /// <summary>One connection id.</summary>
    Connection = 1,
    /// <summary>Connections in a session.</summary>
    Session = 2,
    /// <summary>Members of a broadcast group.</summary>
    Group = 3
}

/// <summary>Backplane message carrying the recipient selector, action name, and optional JSON data.</summary>
/// <param name="Target">Recipient selector.</param>
/// <param name="TargetId">Connection, session, or group key; null for all recipients.</param>
/// <param name="Action">Application action name.</param>
/// <param name="Data">Optional result or notification data.</param>
public sealed record DarkWsBroadcast(
    [property: JsonPropertyName("target")] DarkWsTarget Target,
    [property: JsonPropertyName("targetId")] string? TargetId,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("data")] JsonElement? Data
);
