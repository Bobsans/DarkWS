using DarkWS.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DarkWS.Redis;

public static class RedisConfiguration {
    public static IServiceCollection AddDarkWsRedis(
        this IServiceCollection services,
        string channel
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        services.AddSingleton(new RedisDarkWsOptions(channel));
        services.Replace(ServiceDescriptor.Singleton<IDarkWsBackplane, RedisDarkWsBackplane>());
        return services;
    }
}
