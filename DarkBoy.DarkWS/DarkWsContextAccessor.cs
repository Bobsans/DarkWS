using DarkBoy.DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace DarkBoy.DarkWS;

internal sealed class DarkWsContextAccessor : IDarkWsContextAccessor {
    public IDarkWsSession? Session => Connection.Session;
    public HttpContext HttpContext { get; private set; } = null!;
    public ISession? AspNetSession => HttpContext.Features.Get<ISessionFeature>()?.Session;
    public IWebSocketConnection Connection { get; private set; } = null!;
    public CancellationToken ConnectionAborted { get; private set; }

    public void Initialize(IWebSocketConnection connection, CancellationToken cancellationToken) {
        Connection = connection;
        HttpContext = connection.HttpContext;
        ConnectionAborted = cancellationToken;
    }
}
