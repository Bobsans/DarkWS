using DarkWS.Client;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Optional registration of a single shared DarkWS client session.</summary>
public static class DarkWsClientServiceCollectionExtensions {
    /// <summary>Registers a lazy singleton IDarkWsClient owned by the container. Duplicate registration is rejected.</summary>
    public static IServiceCollection AddDarkWsClient(this IServiceCollection services, Action<DarkWsClientOptions> configure) {
        ArgumentNullException.ThrowIfNull(configure);
        return services.AddDarkWsClient((_, options) => configure(options));
    }

    /// <summary>Registers a lazy singleton with access to application services. Do not capture scoped services.</summary>
    public static IServiceCollection AddDarkWsClient(this IServiceCollection services, Action<IServiceProvider, DarkWsClientOptions> configure) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IDarkWsClient)))
            throw new InvalidOperationException("IDarkWsClient is already registered. Use separate explicit registrations for multiple sessions.");
        services.AddSingleton<IDarkWsClient>(provider => {
            var options = new DarkWsClientOptions();
            configure(provider, options);
            return new DarkWsClient(options);
        });
        return services;
    }
}
