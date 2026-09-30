namespace DarkWS.Redis;

internal sealed record RedisDarkWsOptions(string Channel, int QueueCapacity, int MaxMessageSizeBytes);
