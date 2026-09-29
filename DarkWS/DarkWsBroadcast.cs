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
    Group = 3,

    /// <summary>The union of several groups, optionally excluding connections or sessions.</summary>
    Groups = 4
}

/// <summary>Excludes a connection, every connection in a session, or both from a group broadcast.</summary>
public sealed class DarkWsBroadcastExclusion {
    /// <summary>Gets the connection id to exclude, or null.</summary>
    [JsonPropertyName("connectionId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConnectionId { get; init; }

    /// <summary>Gets the session id whose connections are excluded, or null.</summary>
    [JsonPropertyName("sessionId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SessionId { get; init; }
}

/// <summary>Backplane message carrying the recipient selector, action name, and optional JSON data.</summary>
/// <param name="Target">Recipient selector.</param>
/// <param name="TargetId">Connection, session, or group key; null for all recipients.</param>
/// <param name="Action">Application action name.</param>
/// <param name="Data">Optional result or notification data.</param>
public sealed record DarkWsBroadcast(
    [property: JsonPropertyName("target")] DarkWsTarget Target,
    [property: JsonPropertyName("targetId")]
    string? TargetId,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("data"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonConverter(typeof(BroadcastDataConverter))]
    JsonElement? Data
) : IJsonOnDeserialized {
    /// <summary>Gets the group union for Target Groups. Null for legacy single-target messages.</summary>
    [JsonPropertyName("groups"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Groups { get; init; }

    /// <summary>Gets optional exclusions for Target Groups.</summary>
    [JsonPropertyName("except"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DarkWsBroadcastExclusion? Except { get; init; }

    void IJsonOnDeserialized.OnDeserialized() => ValidateGroupSelection();

    internal void ValidateGroupSelection() {
        if (Target != DarkWsTarget.Groups) {
            if (Groups is not null || Except is not null) {
                throw new ArgumentException("Group lists and exclusions require the Groups target.");
            }
            return;
        }
        if (TargetId is not null || Groups is null || Groups.Any(string.IsNullOrWhiteSpace) || string.IsNullOrWhiteSpace(Action)) {
            throw new ArgumentException("The Groups target requires a group list with non-empty names and no target id.");
        }
        if (Except is not null &&
            ((Except.ConnectionId is not null && string.IsNullOrWhiteSpace(Except.ConnectionId)) ||
             (Except.SessionId is not null && string.IsNullOrWhiteSpace(Except.SessionId)))) {
            throw new ArgumentException("Exclusion ids must be non-empty when provided.");
        }
    }
}

// No data is omitted and JSON null reads back as a Null element, so an explicit null survives a serializing backplane.
internal sealed class BroadcastDataConverter : JsonConverter<JsonElement?> {
    public override bool HandleNull => true;

    public override JsonElement? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return JsonElement.ParseValue(ref reader);
    }

    public override void Write(Utf8JsonWriter writer, JsonElement? value, JsonSerializerOptions options) {
        if (value is { } element) {
            element.WriteTo(writer);
        } else {
            writer.WriteNullValue();
        }
    }
}
