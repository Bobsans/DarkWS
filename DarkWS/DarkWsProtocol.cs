namespace DarkWS;

/// <summary>Reserved identifiers in the DarkWS wire protocol.</summary>
public static class DarkWsProtocol {
    /// <summary>Identifies an unsolicited broadcast; clients must not use it as a request id.</summary>
    public const string BroadcastId = "@";
    /// <summary>Identifies the acknowledgement of a legacy text authentication command.</summary>
    public const string LegacyAuthenticationId = "@auth";

    internal static bool IsReservedRequestId(string id) => id is BroadcastId or LegacyAuthenticationId;
}
