using DarkWS.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DarkWS.Redis;

/// <summary>Registers Redis broadcast delivery using a host-owned connection multiplexer.</summary>
public static class DarkWsRedisServiceCollectionExtensions {
    /// <summary>Selects a Redis backplane on a non-empty literal channel. Register a host-owned IConnectionMultiplexer; DarkWS does not dispose it.</summary>
    public static IServiceCollection AddDarkWsRedis(
        this IServiceCollection services,
        string channel
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        services.AddSingleton(new RedisDarkWsOptions(channel));
        services.Replace(ServiceDescriptor.Singleton<IDarkWsBackplane, RedisDarkWsBackplane>());
        return services;
    }
}
