using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkWS;

/// <summary>Backplane message carrying the recipient selector, action name, and optional JSON data. Property names match BroadcastTarget; JSON field names are the wire protocol.</summary>
/// <param name="TargetType">Recipient kind.</param>
/// <param name="TargetId">Connection, session, or group key; null for All and Groups.</param>
/// <param name="Action">Application action name.</param>
/// <param name="Data">Optional result or notification data.</param>
public sealed record BroadcastMessage(
    [property: JsonPropertyName("target")] BroadcastTargetType TargetType,
    [property: JsonPropertyName("targetId")]
    string? TargetId,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("data"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonConverter(typeof(BroadcastDataConverter))]
    JsonElement? Data
) : IJsonOnDeserialized {
    /// <summary>Gets the group union of a Groups message. Null for single-target messages.</summary>
    [JsonPropertyName("groups"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? GroupNames { get; init; }

    /// <summary>Gets optional exclusions of a Groups message.</summary>
    [JsonPropertyName("except"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BroadcastExclusion? Except { get; init; }

    void IJsonOnDeserialized.OnDeserialized() => ValidateGroupSelection();

    internal void ValidateGroupSelection() {
        if (TargetType != BroadcastTargetType.Groups) {
            if (GroupNames is not null || Except is not null) {
                throw new ArgumentException("Group lists and exclusions require the Groups target.");
            }
            return;
        }
        if (TargetId is not null || GroupNames is null || GroupNames.Any(string.IsNullOrWhiteSpace) || string.IsNullOrWhiteSpace(Action)) {
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
