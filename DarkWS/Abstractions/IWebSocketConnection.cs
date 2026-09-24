using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;

namespace DarkWS.Abstractions;

/// <summary>Public connection contract implementable by consumers and test doubles. Send buffers must not be mutated.</summary>
public interface IWebSocketConnection : IDisposable {
    /// <summary>Gets the stable identity for connection or session targeting.</summary>
    string Id { get; }
    /// <summary>Gets the owned transport. Use connection methods to preserve write serialization and disposal coordination.</summary>
    WebSocket WebSocket { get; }
    /// <summary>Gets the HTTP upgrade context shared by the connection.</summary>
    HttpContext HttpContext { get; }
    /// <summary>Gets the current session, or null for anonymous access.</summary>
    IDarkWsSession? Session { get; }
    /// <summary>Reports whether the transport is open and has not begun closing.</summary>
    bool IsOpen { get; }

    /// <summary>Receives a complete message. Close frames discard partial data; exceeding the size limit closes with status 1009.</summary>
    Task<ReceivedMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default);
    /// <summary>Sends immutable bytes with serialized writes. The built-in transport bounds lock wait and send time and aborts on timeout; closing connections ignore new writes.</summary>
    Task SendAsync(byte[] data, CancellationToken cancellationToken = default);
    /// <summary>Stops new writes and performs graceful close. Supply cancellation to bound the close handshake.</summary>
    Task CloseAsync(CancellationToken cancellationToken = default);
    /// <summary>Aborts the transport immediately, for example after a broadcast timeout. Transportless implementations override it.</summary>
    void Abort() => WebSocket.Abort();
}
