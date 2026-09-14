using System.Security.Claims;
using DarkWS.Abstractions;

namespace DarkWS;

/// <summary>Default session for an authenticated ASP.NET principal, without broadcast groups.</summary>
/// <param name="Id">Correlation or identity key.</param>
/// <param name="User">Authenticated principal.</param>
public sealed record AspNetDarkWsSession(string Id, ClaimsPrincipal User) : IDarkWsSession {
    /// <summary>Gets broadcast groups; storage snapshots membership on add or re-authentication.</summary>
    public IReadOnlyCollection<string> Groups { get; } = [];
}
