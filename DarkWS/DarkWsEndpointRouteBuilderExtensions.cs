using System.Security.Claims;
using DarkWS.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DarkWS;

/// <summary>Maps DarkWS endpoints in the ASP.NET Core request pipeline.</summary>
public static class DarkWsEndpointRouteBuilderExtensions {
    /// <summary>Maps an authenticated WebSocket endpoint with resource limits and liveness detection. Requires UseWebSockets.</summary>
    public static IEndpointConventionBuilder MapDarkWs(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/ws"
    ) {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
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
            // HttpContext.User follows the authenticator's decision, as it does after auth:/logout.
            context.User = session?.User ?? new ClaimsPrincipal(new ClaimsIdentity());

            var socket = await context.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext {
                KeepAliveInterval = options.KeepAliveInterval,
#if NET9_0_OR_GREATER
                KeepAliveTimeout = options.KeepAliveTimeout
#endif
            });
            var connection = new WebSocketConnection(socket, context, session, options.MaxMessageSizeBytes) {
                SendTimeout = options.SendTimeout,
#if NET8_0
                ReceiveIdleTimeout = options.ReceiveIdleTimeout
#endif
            };
            await context.RequestServices.GetRequiredService<WebSocketHandler>()
                .AcceptAsync(connection, cancellation.Token);
        });
    }

}
