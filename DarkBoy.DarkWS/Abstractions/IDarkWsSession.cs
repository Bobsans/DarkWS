using System.Security.Claims;

namespace DarkBoy.DarkWS.Abstractions;

public interface IDarkWsSession {
    string Id { get; }
    ClaimsPrincipal User { get; }
    IReadOnlyCollection<string> Groups { get; }
}
