using System.Security.Claims;

namespace DarkWS.Abstractions;

/// <summary>Application session identity, principal, and groups. Membership is snapshotted on add or re-authentication.</summary>
public interface IDarkWsSession {
    /// <summary>Gets the stable identity for connection or session targeting.</summary>
    string Id { get; }
    /// <summary>Gets the principal used for action authorization.</summary>
    ClaimsPrincipal User { get; }
    /// <summary>Gets broadcast groups; storage snapshots membership on add or re-authentication.</summary>
    IReadOnlyCollection<string> Groups { get; }
}
