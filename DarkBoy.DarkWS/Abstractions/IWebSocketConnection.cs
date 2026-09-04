using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;

namespace DarkBoy.DarkWS.Abstractions;

public interface IWebSocketConnection : IDisposable {
    string Id { get; }
    WebSocket WebSocket { get; }
    HttpContext HttpContext { get; }
    IDarkWsSession? Session { get; }
    bool IsOpen { get; }

    Task<ReceivedMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default);
    Task SendAsync(byte[] data, CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);

    internal void SetSession(IDarkWsSession? session);
}
