using System.Security.Claims;
using DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;

namespace DarkWS.Test.Project;

public sealed class TestAuthenticator : IDarkWsAuthenticator {
    public ValueTask<IDarkWsSession?> AuthenticateAsync(
        HttpContext context,
        string? token,
        CancellationToken cancellationToken
    ) {
        if (string.IsNullOrWhiteSpace(token)) {
            return ValueTask.FromResult<IDarkWsSession?>(null);
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, token)], "Test");
        return ValueTask.FromResult<IDarkWsSession?>(new TestSession(token, new ClaimsPrincipal(identity)));
    }
}
