using System.Security.Claims;
using DarkWS.Abstractions;

namespace DarkWS.Test.Project;

public sealed record TestSession(string Id, ClaimsPrincipal User) : IDarkWsSession {
    public IReadOnlyCollection<string> Groups => [$"session:{Id}"];
}
