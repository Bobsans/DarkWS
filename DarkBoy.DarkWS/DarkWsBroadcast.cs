using System.Text.Json;

namespace DarkBoy.DarkWS;

public enum DarkWsTarget {
    All,
    Connection,
    Session,
    Group
}

public sealed record DarkWsBroadcast(
    DarkWsTarget Target,
    string? TargetId,
    string Action,
    JsonElement? Data
);
