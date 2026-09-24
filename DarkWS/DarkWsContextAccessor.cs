using DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace DarkWS;

internal sealed class DarkWsContextAccessor : IDarkWsContextAccessor {
    // A snapshot: auth:/logout arriving while an action runs must not change that action's identity.
    public IDarkWsSession? Session {
        get { _ = Connection; return _session; }
    }
    private IWebSocketConnection? _connection;
    private IDarkWsSession? _session;
    private CancellationToken _connectionAborted;
    public HttpContext HttpContext => Connection.HttpContext;
    public ISession? AspNetSession => HttpContext.Features.Get<ISessionFeature>()?.Session;
    public IWebSocketConnection Connection => _connection
        ?? throw new InvalidOperationException("DarkWS context is available only in a message scope or through the context passed to a lifecycle hook");
    public CancellationToken ConnectionAborted {
        get { _ = Connection; return _connectionAborted; }
    }

    public void Initialize(IWebSocketConnection connection, CancellationToken cancellationToken, IDarkWsSession? session) {
        _connection = connection;
        _session = session;
        _connectionAborted = cancellationToken;
    }
}
