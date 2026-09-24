using System.Security.Claims;
using DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace DarkWS;

// Uses the HTTP identity only: a token passed to auth: is not validated.
internal sealed class AspNetDarkWsAuthenticator : IDarkWsAuthenticator {
    private static readonly object _fallbackIdKey = new();

    public ValueTask<IDarkWsSession?> AuthenticateAsync(
        HttpContext context,
        string? token,
        CancellationToken cancellationToken
    ) {
        if (context.User.Identity?.IsAuthenticated != true) {
            return ValueTask.FromResult<IDarkWsSession?>(null);
        }

        // The fallback id stays the same across re-authentication, so session broadcasts keep reaching the connection.
        var id = context.User.FindFirst("sid")?.Value
            ?? context.Features.Get<ISessionFeature>()?.Session.Id
            ?? (string)(context.Items[_fallbackIdKey] ??= Guid.NewGuid().ToString("N"));
        return ValueTask.FromResult<IDarkWsSession?>(new AspNetDarkWsSession(id, context.User));
    }
}
