using Microsoft.AspNetCore.Http;

namespace DarkWS.Abstractions;

/// <summary>Resolves a session for a request or replacement token. Return null on rejection; expiry remains application policy.</summary>
public interface IDarkWsAuthenticator {
    /// <summary>Resolves a session for an HTTP request and optional token, or null on rejection.</summary>
    ValueTask<IDarkWsSession?> AuthenticateAsync(
        HttpContext context,
        string? token,
        CancellationToken cancellationToken
    );
}
