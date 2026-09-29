using Microsoft.AspNetCore.Http;

namespace DarkWS.Abstractions;

/// <summary>Public connection contract implementable by consumers and test doubles. Send buffers are shared between recipients and must not be mutated.</summary>
public interface IWebSocketConnection : IDisposable {
    /// <summary>Gets the stable identity for connection or session targeting.</summary>
    string Id { get; }

    /// <summary>Gets the HTTP upgrade context shared by the connection.</summary>
    HttpContext HttpContext { get; }

    /// <summary>Gets the current session, or null for anonymous access.</summary>
    IDarkWsSession? Session { get; }

    /// <summary>Reports whether the transport is open and has not begun closing.</summary>
    bool IsOpen { get; }

    /// <summary>Sends one complete UTF-8 text message with serialized writes. The built-in transport bounds lock wait and send time and aborts on timeout; closing connections ignore new writes.</summary>
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>Stops new writes and performs graceful close. Supply cancellation to bound the close handshake.</summary>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>Aborts the transport immediately, for example after a broadcast timeout.</summary>
    void Abort();
}
