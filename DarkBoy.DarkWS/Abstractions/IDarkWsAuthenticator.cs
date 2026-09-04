using Microsoft.AspNetCore.Http;

namespace DarkBoy.DarkWS.Abstractions;

public interface IDarkWsAuthenticator {
    ValueTask<IDarkWsSession?> AuthenticateAsync(
        HttpContext context,
        string? token,
        CancellationToken cancellationToken
    );
}
