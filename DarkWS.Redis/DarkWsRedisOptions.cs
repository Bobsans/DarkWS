namespace DarkWS.Redis;

/// <summary>Bounds Redis broadcast buffering and serialized envelope sizes.</summary>
public sealed class DarkWsRedisOptions {
    /// <summary>Gets or sets the maximum queued messages per subscription. Default 256; must be positive. A full queue drops new messages.</summary>
    public int QueueCapacity { get; set; } = 256;

    /// <summary>Gets or sets the maximum serialized envelope size in bytes. Default 1 MiB; must be positive. Oversize publications are rejected and incoming messages are dropped.</summary>
    public int MaxMessageSizeBytes { get; set; } = 1024 * 1024;
}
