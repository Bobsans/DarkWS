using DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;

namespace DarkWS;

public abstract class HandlerBase {
    private WebSocketHandler Owner { get; set; } = null!;
    private IDarkWsContextAccessor Context { get; set; } = null!;

    protected IDarkWsSession Session => Context.Session
        ?? throw new InvalidOperationException("This handler requires an authenticated session");
    protected HttpContext HttpContext => Context.HttpContext;
    protected ISession? AspNetSession => Context.AspNetSession;
    protected IWebSocketConnection Connection => Context.Connection;
    protected CancellationToken ConnectionAborted => Context.ConnectionAborted;

    internal void Initialize(WebSocketHandler owner, IDarkWsContextAccessor context) {
        Owner = owner;
        Context = context;
    }

    protected static IResponse Ok() => new SuccessResponse();
    protected static IResponse Ok<T>(T data) => new SuccessResponse<T>(data);
    protected static IResponse Error(string error) => new ErrorResponse(error);
    protected static IResponse Error<T>(string error, T details) => new ErrorResponse<T>(error, details);

    protected Task BroadcastAsync(string action, CancellationToken cancellationToken = default) {
        return Owner.Broadcaster.BroadcastAsync(action, cancellationToken);
    }

    protected Task BroadcastAsync<T>(string action, T? data, CancellationToken cancellationToken = default) {
        return Owner.Broadcaster.BroadcastAsync(action, data, cancellationToken);
    }

    protected Task BroadcastToSelfAsync(string action, CancellationToken cancellationToken = default) {
        return Owner.Broadcaster.BroadcastToConnectionAsync(Connection.Id, action, cancellationToken);
    }

    protected Task BroadcastToSelfAsync<T>(string action, T? data, CancellationToken cancellationToken = default) {
        return Owner.Broadcaster.BroadcastToConnectionAsync(Connection.Id, action, data, cancellationToken);
    }

    protected Task BroadcastToSessionAsync(string sessionId, string action, CancellationToken cancellationToken = default) {
        return Owner.Broadcaster.BroadcastToSessionAsync(sessionId, action, cancellationToken);
    }

    protected Task BroadcastToSessionAsync<T>(string sessionId, string action, T? data, CancellationToken cancellationToken = default) {
        return Owner.Broadcaster.BroadcastToSessionAsync(sessionId, action, data, cancellationToken);
    }

    protected Task BroadcastToGroupAsync(string group, string action, CancellationToken cancellationToken = default) {
        return Owner.Broadcaster.BroadcastToGroupAsync(group, action, cancellationToken);
    }

    protected Task BroadcastToGroupAsync<T>(string group, string action, T? data, CancellationToken cancellationToken = default) {
        return Owner.Broadcaster.BroadcastToGroupAsync(group, action, data, cancellationToken);
    }
}

public abstract class HandlerBase<TSession> : HandlerBase
    where TSession : class, IDarkWsSession {
    protected new TSession Session => base.Session as TSession
        ?? throw new InvalidOperationException($"Expected session type {typeof(TSession).FullName}");
}
