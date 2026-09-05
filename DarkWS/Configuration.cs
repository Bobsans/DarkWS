using DarkWS.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DarkWS;

public static class Configuration {
    public static DarkWsBuilder AddDarkWs(
        this IServiceCollection services,
        Action<DarkWsOptions>? configure = null
    ) {
        var options = new DarkWsOptions();
        configure?.Invoke(options);
        var registry = new DarkWsActionRegistry();

        services.AddSingleton(Options.Create(options));
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

    public static IEndpointConventionBuilder MapDarkWs(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/ws"
    ) {
        return endpoints.Map(pattern, async context => {
            if (!context.WebSockets.IsWebSocketRequest) {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var options = context.RequestServices.GetRequiredService<IOptions<DarkWsOptions>>().Value;
            var lifetime = context.RequestServices.GetRequiredService<IHostApplicationLifetime>();
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                context.RequestAborted,
                lifetime.ApplicationStopping
            );

            var token = context.Request.Query[options.AuthenticationQueryParameter].FirstOrDefault();
            var authenticator = context.RequestServices.GetRequiredService<IDarkWsAuthenticator>();
            var session = await authenticator.AuthenticateAsync(context, token, cancellation.Token);
            if (session is not null) {
                context.User = session.User;
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext {
                KeepAliveInterval = options.KeepAliveInterval
            });
            var connection = new WebSocketConnection(socket, context, session);
            await context.RequestServices.GetRequiredService<WebSocketHandler>()
                .AcceptAsync(connection, cancellation.Token);
        });
    }
}
