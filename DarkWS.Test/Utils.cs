using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DarkWS.Test;

public static class Utils {
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public static Task SendMessage<T>(this WebSocket webSocket, T message) {
        return webSocket.SendAsync(
            JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }

    public static Task SendTextAsync(this WebSocket webSocket, string message) {
        return webSocket.SendAsync(
            Encoding.UTF8.GetBytes(message),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }

    public static async Task<byte[]> ReceiveRawMessage(this WebSocket webSocket) {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        try {
            // A message larger than the buffer arrives in several reads.
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do {
                result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                message.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);
            return message.ToArray();
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static async Task<T?> ReceiveMessage<T>(this WebSocket webSocket) {
        return JsonSerializer.Deserialize<T>(await webSocket.ReceiveRawMessage(), JsonOptions);
    }
}
