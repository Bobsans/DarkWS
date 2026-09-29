using DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace DarkWS;

internal sealed class DarkWsContextAccessor : IDarkWsContextAccessor {
    internal IServiceProvider Services { get; private set; } = null!;
    internal IBroadcaster Broadcaster { get; private set; } = null!;

    // A snapshot: auth:/logout arriving while an action runs must not change that action's identity.
    public IDarkWsSession? Session {
        get {
            _ = Connection;
            return _session;
        }
    }

    private IWebSocketConnection? _connection;
    private IDarkWsSession? _session;
    private DarkWsActionInfo? _action;
    private CancellationToken _connectionAborted;
    public HttpContext HttpContext => Connection.HttpContext;
    public ISession? AspNetSession => HttpContext.Features.Get<ISessionFeature>()?.Session;

    public IWebSocketConnection Connection => _connection
        ?? throw new InvalidOperationException("DarkWS context is available only in a message scope or through the context passed to a lifecycle hook");

    public CancellationToken ConnectionAborted {
        get {
            _ = Connection;
            return _connectionAborted;
        }
    }

    public DarkWsActionInfo? Action {
        get {
            _ = Connection;
            return _action;
        }
    }

    public void Initialize(IWebSocketConnection connection, CancellationToken cancellationToken, IDarkWsSession? session,
        IServiceProvider services, IBroadcaster broadcaster, DarkWsActionInfo? action = null) {
        _connection = connection;
        _session = session;
        _action = action;
        _connectionAborted = cancellationToken;
        Services = services;
        Broadcaster = broadcaster;
    }
}
