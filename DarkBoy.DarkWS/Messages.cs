using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkBoy.DarkWS;

public sealed record InputMessage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("payload")] JsonElement? Payload
);

public sealed record OkMessage(
    [property: JsonPropertyName("id")] string Id
);

public sealed record ResponseMessage<T>(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("data")] T? Data
);

public sealed record ErrorMessage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("error")] string Error
);

public sealed record ErrorMessage<T>(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("data")] T? Data
);

public sealed record BroadcastActionMessage(
    [property: JsonPropertyName("action")] string Action
);

public sealed record BroadcastActionMessage<T>(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("data")] T? Data
);
