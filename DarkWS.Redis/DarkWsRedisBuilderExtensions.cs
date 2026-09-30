using DarkWS.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DarkWS.Redis;

/// <summary>Registers Redis broadcast delivery using a host-owned connection multiplexer.</summary>
public static class DarkWsRedisBuilderExtensions {
    /// <summary>Selects a Redis backplane on a non-empty literal channel. Register a host-owned IConnectionMultiplexer; DarkWS does not dispose it. A second call throws.</summary>
    public static DarkWsBuilder AddRedis(this DarkWsBuilder builder, string channel) => AddRedis(builder, channel, null);

    /// <summary>Selects a Redis backplane with explicit queue and envelope limits. Settings are validated and snapshotted at registration.</summary>
    public static DarkWsBuilder AddRedis(this DarkWsBuilder builder, string channel, Action<DarkWsRedisOptions>? configure) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        var services = builder.Services;
        // A second channel would silently replace the first, so a repeated call is a configuration error.
        if (services.Any(service => service.ServiceType == typeof(RedisDarkWsOptions))) {
            throw new InvalidOperationException("AddRedis must only be called once per service collection");
        }

        var limits = new DarkWsRedisOptions();
        configure?.Invoke(limits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limits.QueueCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limits.MaxMessageSizeBytes);
        services.AddSingleton(new RedisDarkWsOptions(channel, limits.QueueCapacity, limits.MaxMessageSizeBytes));
        services.Replace(ServiceDescriptor.Singleton<IDarkWsBackplane, RedisDarkWsBackplane>());
        return builder;
    }
}
