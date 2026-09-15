namespace DarkWS;

/// <summary>Reserved identifiers in the DarkWS wire protocol.</summary>
public static class DarkWsProtocol {
    /// <summary>Identifies an unsolicited broadcast; clients must not use it as a request id.</summary>
    public const string BroadcastId = "@";
    /// <summary>Reserved legacy acknowledgement id. Current system replies are plain text and do not emit this id.</summary>
    public const string LegacyAuthenticationId = "@auth";

    internal static bool IsReservedRequestId(string id) => id is BroadcastId or LegacyAuthenticationId;
}
