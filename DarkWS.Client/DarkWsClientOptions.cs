using System.Net.WebSockets;
using System.Text.Json;

namespace DarkWS.Client;

/// <summary>Settings captured when a client is constructed.</summary>
public sealed class DarkWsClientOptions {
    /// <summary>Absolute ws or wss endpoint, without user information or a fragment.</summary>
    public Uri Endpoint { get; set; } = null!;
    /// <summary>Maximum connection attempt and connection wait duration. Default: 30 seconds.</summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Maximum send queue and write duration. Default: 30 seconds.</summary>
    public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Response timeout after sending. Default: five minutes; InfiniteTimeSpan disables expiry.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(5);
    /// <summary>Maximum graceful close duration. Default: five seconds.</summary>
    public TimeSpan CloseTimeout { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>Whether transport failures trigger reconnect. Default: true.</summary>
    public bool Reconnect { get; set; } = true;
    /// <summary>Interval before sending a text ping. Default: 30 seconds.</summary>
    public TimeSpan PingInterval { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Maximum pong wait after writing a ping. Default: 30 seconds.</summary>
    public TimeSpan PongTimeout { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Maximum incoming message size, including all fragments. Default: 1 MiB.</summary>
    public int MaxMessageSizeBytes { get; set; } = 1024 * 1024;
    /// <summary>Maximum application requests, including connection waits. Default: 256.</summary>
    public int MaxPendingRequests { get; set; } = 256;
    /// <summary>Maximum queued broadcasts. Default: 256.</summary>
    public int NotificationQueueCapacity { get; set; } = 256;
    /// <summary>Payload and result serializer options. The client takes a copy. Default: Web defaults.</summary>
    public JsonSerializerOptions JsonOptions { get; set; } = new(JsonSerializerDefaults.Web);
    /// <summary>Configures each fresh socket before connecting. Must honor cancellation.</summary>
    public Func<ClientWebSocketOptions, CancellationToken, ValueTask>? ConfigureWebSocketOptionsAsync { get; set; }
    /// <summary>Gets a nonempty token for acknowledged authentication on each connection. Must honor cancellation.</summary>
    public Func<CancellationToken, ValueTask<string?>>? AuthenticationTokenProvider { get; set; }

    internal DarkWsClientOptions Snapshot() {
        if (Endpoint is null || !Endpoint.IsAbsoluteUri || Endpoint.Scheme is not ("ws" or "wss") ||
            Endpoint.Fragment.Length != 0 || Endpoint.UserInfo.Length != 0)
            throw new ArgumentException("Endpoint must be an absolute ws/wss URI without user information or a fragment.", nameof(Endpoint));
        ValidateTimeout(ConnectionTimeout, nameof(ConnectionTimeout));
        ValidateTimeout(SendTimeout, nameof(SendTimeout));
        if (RequestTimeout != Timeout.InfiniteTimeSpan) ValidateTimeout(RequestTimeout, nameof(RequestTimeout));
        ValidateTimeout(CloseTimeout, nameof(CloseTimeout));
        ValidateTimeout(PingInterval, nameof(PingInterval));
        ValidateTimeout(PongTimeout, nameof(PongTimeout));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxMessageSizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxPendingRequests);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(NotificationQueueCapacity);
        ArgumentNullException.ThrowIfNull(JsonOptions);
        var copy = (DarkWsClientOptions)MemberwiseClone();
        copy.JsonOptions = new JsonSerializerOptions(JsonOptions);
        return copy;
    }

    private static void ValidateTimeout(TimeSpan value, string name) {
        if (value.TotalMilliseconds < 1 || value.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(name, "Timeout must be between 1 and 4294967294 milliseconds.");
    }
}
