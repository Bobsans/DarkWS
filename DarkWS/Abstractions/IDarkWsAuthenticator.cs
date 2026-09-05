using Microsoft.AspNetCore.Http;

namespace DarkWS.Abstractions;

public interface IDarkWsAuthenticator {
    ValueTask<IDarkWsSession?> AuthenticateAsync(
        HttpContext context,
        string? token,
        CancellationToken cancellationToken
    );
}
