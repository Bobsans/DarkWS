using System.Security.Claims;
using DarkBoy.DarkWS.Abstractions;

namespace DarkBoy.DarkWS.Test.Project;

public sealed record TestSession(string Id, ClaimsPrincipal User) : IDarkWsSession {
    public IReadOnlyCollection<string> Groups => [$"session:{Id}"];
}
