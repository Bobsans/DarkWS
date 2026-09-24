using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkWS;

/// <summary>Incoming request envelope. Id and Action are required; payload nullability follows the action signature.</summary>
/// <param name="Id">Correlation or identity key.</param>
/// <param name="Action">Application action name.</param>
/// <param name="Payload">Optional request data, serialized as the data field.</param>
public sealed record InputMessage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("data")] JsonElement? Payload
);

/// <summary>Successful response without data, correlated by request id.</summary>
/// <param name="Id">Correlation or identity key.</param>
public sealed record OkMessage(
    [property: JsonPropertyName("id")] string Id
);

/// <summary>Successful response carrying correlated result data.</summary>
/// <param name="Id">Correlation or identity key.</param>
/// <param name="Data">Optional result or notification data.</param>
public sealed record ResponseMessage<T>(
    [property: JsonPropertyName("id")] string Id,
    // Written even under WhenWritingNull: a missing data field means "no data" to clients.
    [property: JsonPropertyName("data"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] T? Data
);

/// <summary>Error response carrying a stable code and optional typed details.</summary>
/// <param name="Id">Correlation or identity key.</param>
/// <param name="Error">Stable error code.</param>
public sealed record ErrorMessage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("error")] string Error
);

/// <summary>Error response carrying a stable code and optional typed details.</summary>
/// <param name="Id">Correlation or identity key.</param>
/// <param name="Error">Stable error code.</param>
/// <param name="Data">Optional result or notification data.</param>
public sealed record ErrorMessage<T>(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("data"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] T? Data
);

/// <summary>Notification envelope containing the reserved id and application action.</summary>
/// <param name="Action">Application action name.</param>
public sealed record BroadcastActionMessage(
    [property: JsonPropertyName("action")] string Action
) {
    /// <summary>Reserved notification id.</summary>
    [JsonPropertyName("id"), JsonPropertyOrder(-1)]
    public string Id => DarkWsProtocol.BroadcastId;
}

/// <summary>Notification envelope containing the reserved id, application action, and data.</summary>
/// <param name="Action">Application action name.</param>
/// <param name="Data">Optional result or notification data.</param>
public sealed record BroadcastActionMessage<T>(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("data"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] T? Data
) {
    /// <summary>Reserved notification id.</summary>
    [JsonPropertyName("id"), JsonPropertyOrder(-1)]
    public string Id => DarkWsProtocol.BroadcastId;
}
