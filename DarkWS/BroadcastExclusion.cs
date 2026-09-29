using System.Text.Json.Serialization;

namespace DarkWS;

/// <summary>Excludes a connection, every connection in a session, or both from a group broadcast.</summary>
public sealed class BroadcastExclusion {
    /// <summary>Gets the connection id to exclude, or null.</summary>
    [JsonPropertyName("connectionId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConnectionId { get; init; }

    /// <summary>Gets the session id whose connections are excluded, or null.</summary>
    [JsonPropertyName("sessionId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SessionId { get; init; }
}
