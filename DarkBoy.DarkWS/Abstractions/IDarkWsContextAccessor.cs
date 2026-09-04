using Microsoft.AspNetCore.Http;

namespace DarkBoy.DarkWS.Abstractions;

public interface IDarkWsContextAccessor {
    IDarkWsSession? Session { get; }
    HttpContext HttpContext { get; }
    ISession? AspNetSession { get; }
    IWebSocketConnection Connection { get; }
    CancellationToken ConnectionAborted { get; }
}
