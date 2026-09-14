using System.Net.WebSockets;

namespace DarkWS;

/// <summary>Complete received message, or a close result with an empty data buffer.</summary>
/// <param name="result">Transport receive metadata.</param>
/// <param name="data">Result or message data.</param>
public class ReceivedMessage(
    WebSocketReceiveResult result,
    byte[] data
) : WebSocketReceiveResult((result ?? throw new ArgumentNullException(nameof(result))).Count, result.MessageType, result.EndOfMessage, result.CloseStatus, result.CloseStatusDescription) {
    /// <summary>Gets complete message bytes; empty for close results.</summary>
    public byte[] Data { get; } = data ?? throw new ArgumentNullException(nameof(data));
}
