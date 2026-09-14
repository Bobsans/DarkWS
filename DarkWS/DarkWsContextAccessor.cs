using DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace DarkWS;

internal sealed class DarkWsContextAccessor : IDarkWsContextAccessor {
    public IDarkWsSession? Session => Connection.Session;
    private IWebSocketConnection? _connection;
    private CancellationToken _connectionAborted;
    public HttpContext HttpContext => Connection.HttpContext;
    public ISession? AspNetSession => HttpContext.Features.Get<ISessionFeature>()?.Session;
    public IWebSocketConnection Connection => _connection
        ?? throw new InvalidOperationException("DarkWS context is available only in a message scope or through the context passed to a lifecycle hook");
    public CancellationToken ConnectionAborted {
        get { _ = Connection; return _connectionAborted; }
    }

    public void Initialize(IWebSocketConnection connection, CancellationToken cancellationToken) {
        _connection = connection;
        _connectionAborted = cancellationToken;
    }
}
