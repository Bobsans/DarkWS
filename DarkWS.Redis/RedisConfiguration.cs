using Microsoft.Extensions.DependencyInjection;

namespace DarkWS.Redis;

/// <summary>Compatibility entry point for legacy static Redis registration calls.</summary>
[Obsolete("Use DarkWsRedisServiceCollectionExtensions. This wrapper will be removed in the next major release.")]
public static class RedisConfiguration {
    /// <summary>Forwards the legacy static Redis backplane registration call.</summary>
    public static IServiceCollection AddDarkWsRedis(IServiceCollection services, string channel)
        => DarkWsRedisServiceCollectionExtensions.AddDarkWsRedis(services, channel);
}
