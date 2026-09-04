using System.Net.WebSockets;

namespace DarkBoy.DarkWS;

public class ReceivedMessage(
    WebSocketReceiveResult result,
    byte[] data
) : WebSocketReceiveResult(result.Count, result.MessageType, result.EndOfMessage, result.CloseStatus, result.CloseStatusDescription) {
    public byte[] Data { get; } = data;
}
