using System.Security.Claims;
using DarkBoy.DarkWS.Abstractions;

namespace DarkBoy.DarkWS;

public sealed record AspNetDarkWsSession(string Id, ClaimsPrincipal User) : IDarkWsSession {
    public IReadOnlyCollection<string> Groups { get; } = [];
}
