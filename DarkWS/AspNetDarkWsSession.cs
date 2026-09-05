using System.Security.Claims;
using DarkWS.Abstractions;

namespace DarkWS;

public sealed record AspNetDarkWsSession(string Id, ClaimsPrincipal User) : IDarkWsSession {
    public IReadOnlyCollection<string> Groups { get; } = [];
}
