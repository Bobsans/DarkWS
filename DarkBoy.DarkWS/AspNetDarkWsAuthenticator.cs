using System.Security.Claims;
using DarkBoy.DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace DarkBoy.DarkWS;

internal sealed class AspNetDarkWsAuthenticator : IDarkWsAuthenticator {
    public ValueTask<IDarkWsSession?> AuthenticateAsync(
        HttpContext context,
        string? token,
        CancellationToken cancellationToken
    ) {
        if (context.User.Identity?.IsAuthenticated != true) {
            return ValueTask.FromResult<IDarkWsSession?>(null);
        }

        var id = context.User.FindFirst("sid")?.Value
            ?? context.Features.Get<ISessionFeature>()?.Session.Id
            ?? Guid.NewGuid().ToString("N");
        return ValueTask.FromResult<IDarkWsSession?>(new AspNetDarkWsSession(id, context.User));
    }
}
