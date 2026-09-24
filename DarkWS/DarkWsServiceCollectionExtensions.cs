using DarkWS.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DarkWS;

/// <summary>Registers DarkWS services and validated options.</summary>
public static class DarkWsServiceCollectionExtensions {
    /// <summary>Registers DarkWS once and composes options. Invalid settings fail on resolution or host startup; duplicate registration throws InvalidOperationException.</summary>
    public static DarkWsBuilder AddDarkWs(
        this IServiceCollection services,
        Action<DarkWsOptions>? configure = null
    ) {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(service => service.ServiceType == typeof(DarkWsActionRegistry))) {
            throw new InvalidOperationException("AddDarkWs must only be called once per service collection");
        }

        var options = services.AddOptions<DarkWsOptions>();
        if (configure is not null) options.Configure(configure);
        options.Validate(value => value.MaxMessageSizeBytes > 0, "MaxMessageSizeBytes must be positive")
            .Validate(value => value.MaxConcurrentRequestsPerConnection > 0, "MaxConcurrentRequestsPerConnection must be positive")
            .Validate(value => IsValidTimeout(value.KeepAliveInterval), "KeepAliveInterval must be a positive timer duration")
            .Validate(value => IsValidTimeout(value.KeepAliveTimeout), "KeepAliveTimeout must be a positive timer duration")
            .Validate(value => IsValidTimeout(value.ReceiveIdleTimeout), "ReceiveIdleTimeout must be a positive timer duration")
            .Validate(value => IsValidTimeout(value.SendTimeout), "SendTimeout must be a positive timer duration")
            .Validate(value => IsValidTimeout(value.BroadcastSendTimeout), "BroadcastSendTimeout must be a positive timer duration")
            .Validate(value => IsValidTimeout(value.ShutdownTimeout), "ShutdownTimeout must be a positive timer duration")
            .Validate(value => IsValidTimeout(value.RequestQueueTimeout), "RequestQueueTimeout must be a positive timer duration")
            .Validate(value => value.JsonOptions is not null, "JsonOptions is required")
            .Validate(value => !string.IsNullOrWhiteSpace(value.AuthenticationQueryParameter), "AuthenticationQueryParameter is required")
            .Validate(value => new[] { value.InvalidActionError, value.InvalidRequestError, value.AuthorizationRequiredError, value.RequestFailedError, value.BusyError, value.AuthenticationFailedError }.All(error => !string.IsNullOrWhiteSpace(error)), "Error codes must not be empty")
            .ValidateOnStart();
        var registry = new DarkWsActionRegistry();

        services.AddSingleton(registry);
        services.TryAddSingleton<ConnectionStorage>();
        services.TryAddSingleton<InMemoryDarkWsBackplane>();
        services.TryAddSingleton<IDarkWsBackplane>(provider =>
            provider.GetRequiredService<InMemoryDarkWsBackplane>());
        services.TryAddSingleton<Broadcaster>();
        services.TryAddSingleton<IBroadcaster>(provider => provider.GetRequiredService<Broadcaster>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, DarkWsBackplaneHostedService>());
        services.TryAddScoped<DarkWsContextAccessor>();
        services.TryAddScoped<IDarkWsContextAccessor>(provider =>
            provider.GetRequiredService<DarkWsContextAccessor>());
        services.TryAddScoped<IDarkWsAuthenticator, AspNetDarkWsAuthenticator>();
        services.TryAddScoped<WebSocketHandler>();

        return new DarkWsBuilder(services, registry);
    }

    private static bool IsValidTimeout(TimeSpan value) {
        return value > TimeSpan.Zero && value.TotalMilliseconds <= uint.MaxValue - 1;
    }
}
