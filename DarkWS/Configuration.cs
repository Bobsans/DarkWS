using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DarkWS;

/// <summary>Compatibility entry points; use the dedicated service and endpoint extension classes.</summary>
[Obsolete("Use DarkWsServiceCollectionExtensions and DarkWsEndpointRouteBuilderExtensions. This wrapper will be removed in the next major release.")]
public static class Configuration {
    /// <summary>Forwards the legacy static service registration call.</summary>
    public static DarkWsBuilder AddDarkWs(IServiceCollection services, Action<DarkWsOptions>? configure = null)
        => DarkWsServiceCollectionExtensions.AddDarkWs(services, configure);

    /// <summary>Forwards the legacy static endpoint registration call.</summary>
    public static IEndpointConventionBuilder MapDarkWs(IEndpointRouteBuilder endpoints, string pattern = "/ws")
        => DarkWsEndpointRouteBuilderExtensions.MapDarkWs(endpoints, pattern);
}
