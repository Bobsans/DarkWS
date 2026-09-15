using System.Text.Json;

namespace DarkWS;

/// <summary>Validated startup settings for serialization, limits, and wire errors; supports the standard .NET Options pipeline.</summary>
public sealed class DarkWsOptions {
    /// <summary>Default complete incoming message limit: 1 MiB (1048576 bytes).</summary>
    public const int DefaultMaxMessageSizeBytes = 1024 * 1024;
    /// <summary>Complete incoming message limit including fragments. Default 1 MiB; must be positive.</summary>
    public int MaxMessageSizeBytes { get; set; } = DefaultMaxMessageSizeBytes;
    /// <summary>JSON settings for envelopes and payloads. Defaults to web conventions; must not be null.</summary>
    public JsonSerializerOptions JsonOptions { get; set; } = new(JsonSerializerDefaults.Web);
    /// <summary>Maximum in-flight requests per connection. Default 16; saturation applies read backpressure.</summary>
    public int MaxConcurrentRequestsPerConnection { get; set; } = 16;
    /// <summary>Transport keep-alive interval. Default 30 seconds; must be a positive timer duration.</summary>
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Transport PONG deadline on .NET 9 and later. Default 30 seconds; unused on .NET 8.</summary>
    public TimeSpan KeepAliveTimeout { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Pending-read deadline on .NET 8. Default 2 minutes; data fragments reset it. Idle clients must send application traffic.</summary>
    public TimeSpan ReceiveIdleTimeout { get; set; } = TimeSpan.FromMinutes(2);
    /// <summary>Combined lock wait and socket write deadline. Default 30 seconds; expiration aborts the connection.</summary>
    public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Per-recipient broadcast deadline. Default 10 seconds; expiration aborts that recipient.</summary>
    public TimeSpan BroadcastSendTimeout { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>Shared deadline for cancellation, close hooks, and handshake. Default 10 seconds; uncooperative tasks retain resources until completion.</summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>Connection token query parameter. Default token; must not be blank.</summary>
    public string AuthenticationQueryParameter { get; set; } = "token";
    /// <summary>Unknown action error code. Default darkws:error:invalid-action.</summary>
    public string InvalidActionError { get; set; } = "darkws:error:invalid-action";
    /// <summary>Invalid envelope or payload code. Default darkws:error:invalid-request.</summary>
    public string InvalidRequestError { get; set; } = "darkws:error:invalid-request";
    /// <summary>Authentication-required error code. Default darkws:error:authorization-required.</summary>
    public string AuthorizationRequiredError { get; set; } = "darkws:error:authorization-required";
    /// <summary>Unexpected handler failure code. Default darkws:error:request-failed.</summary>
    public string RequestFailedError { get; set; } = "darkws:error:request-failed";
    /// <summary>Legacy JSON authentication error code. Text authentication always replies auth:failed.</summary>
    public string AuthenticationFailedError { get; set; } = "darkws:error:authentication-failed";
}
