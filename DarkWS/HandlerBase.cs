using DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;

namespace DarkWS;

/// <summary>Base for per-message handlers. Context-dependent members are available only during action invocation.</summary>
public abstract class HandlerBase {
    private DarkWsContextAccessor Context { get; set; } = null!;

    /// <summary>Gets the current message scope's service provider.</summary>
    protected IServiceProvider Services => Context.Services;

    /// <summary>Gets the required handler session. Throws InvalidOperationException for an anonymous connection or incompatible typed session.</summary>
    protected IDarkWsSession Session => Context.Session
        ?? throw new InvalidOperationException("This handler requires an authenticated session");

    /// <summary>Gets the HTTP upgrade context shared by the connection. Not thread-safe: concurrent actions of the connection share it.</summary>
    protected HttpContext HttpContext => Context.HttpContext;

    /// <summary>Gets ASP.NET session state when its middleware is installed, otherwise null. Not thread-safe: concurrent actions of the connection share it.</summary>
    protected ISession? AspNetSession => Context.AspNetSession;

    /// <summary>Gets the current connection during initialized message handling.</summary>
    protected IWebSocketConnection Connection => Context.Connection;

    /// <summary>Gets the token signaled when the connection stops. Handlers should observe it during asynchronous work.</summary>
    protected CancellationToken ConnectionAborted => Context.ConnectionAborted;

    internal void Initialize(DarkWsContextAccessor context) {
        Context = context;
    }

    /// <summary>Creates a successful action result with optional typed data.</summary>
    protected static IResponse Ok() => new SuccessResponse();

    /// <summary>Creates a successful action result with optional typed data.</summary>
    protected static IResponse Ok<T>(T data) => new SuccessResponse<T>(data);

    /// <summary>Creates an error action result with a stable code and optional details.</summary>
    protected static IResponse Error(string error) => new ErrorResponse(error);

    /// <summary>Creates an error action result with a stable code and optional details.</summary>
    protected static IResponse Error<T>(string error, T details) => new ErrorResponse<T>(error, details);

    /// <summary>Publishes an action and optional data to all connections through the backplane.</summary>
    protected Task BroadcastAsync(string action, CancellationToken cancellationToken = default) {
        return Context.Broadcaster.BroadcastAsync(action, cancellationToken);
    }

    /// <summary>Publishes an action and optional data to all connections through the backplane.</summary>
    protected Task BroadcastAsync<T>(string action, T? data, CancellationToken cancellationToken = default) {
        return Context.Broadcaster.BroadcastAsync(action, data, cancellationToken);
    }

    /// <summary>Publishes an action and optional data to the current handler connection.</summary>
    protected Task BroadcastToSelfAsync(string action, CancellationToken cancellationToken = default) {
        return Context.Broadcaster.BroadcastToConnectionAsync(Connection.Id, action, cancellationToken);
    }

    /// <summary>Publishes an action and optional data to the current handler connection.</summary>
    protected Task BroadcastToSelfAsync<T>(string action, T? data, CancellationToken cancellationToken = default) {
        return Context.Broadcaster.BroadcastToConnectionAsync(Connection.Id, action, data, cancellationToken);
    }

    /// <summary>Publishes an action and optional data to connections in the specified session.</summary>
    protected Task BroadcastToSessionAsync(string sessionId, string action, CancellationToken cancellationToken = default) {
        return Context.Broadcaster.BroadcastToSessionAsync(sessionId, action, cancellationToken);
    }

    /// <summary>Publishes an action and optional data to connections in the specified session.</summary>
    protected Task BroadcastToSessionAsync<T>(string sessionId, string action, T? data, CancellationToken cancellationToken = default) {
        return Context.Broadcaster.BroadcastToSessionAsync(sessionId, action, data, cancellationToken);
    }

    /// <summary>Publishes an action and optional data to members of the specified group.</summary>
    protected Task BroadcastToGroupAsync(string group, string action, CancellationToken cancellationToken = default) {
        return Context.Broadcaster.BroadcastToGroupAsync(group, action, cancellationToken);
    }

    /// <summary>Publishes an action and optional data to members of the specified group.</summary>
    protected Task BroadcastToGroupAsync<T>(string group, string action, T? data, CancellationToken cancellationToken = default) {
        return Context.Broadcaster.BroadcastToGroupAsync(group, action, data, cancellationToken);
    }

    /// <summary>Publishes to one group with exclusions; use Connection.Id to exclude this handler's connection.</summary>
    protected Task BroadcastToGroupAsync(string group, string action, DarkWsBroadcastExclusion except, CancellationToken cancellationToken = default) {
        return Context.Broadcaster.BroadcastToGroupAsync(group, action, except, cancellationToken);
    }

    /// <summary>Publishes data to one group with explicit connection/session exclusions.</summary>
    protected Task BroadcastToGroupAsync<T>(string group, string action, T? data, DarkWsBroadcastExclusion except, CancellationToken cancellationToken = default) {
        return Context.Broadcaster.BroadcastToGroupAsync(group, action, data, except, cancellationToken);
    }

    /// <summary>Publishes once to a group union, with optional exclusions and no duplicate recipients.</summary>
    protected Task BroadcastToGroupsAsync(IEnumerable<string> groups, string action, DarkWsBroadcastExclusion? except = null, CancellationToken cancellationToken = default) {
        return Context.Broadcaster.BroadcastToGroupsAsync(groups, action, except, cancellationToken);
    }

    /// <summary>Publishes data once to a group union, with optional exclusions and no duplicate recipients.</summary>
    protected Task BroadcastToGroupsAsync<T>(IEnumerable<string> groups, string action, T? data, DarkWsBroadcastExclusion? except = null, CancellationToken cancellationToken = default) {
        return Context.Broadcaster.BroadcastToGroupsAsync(groups, action, data, except, cancellationToken);
    }
}

/// <summary>Base for per-message handlers. Context-dependent members are available only during action invocation.</summary>
public abstract class HandlerBase<TSession> : HandlerBase
    where TSession : class, IDarkWsSession {
    /// <summary>Gets the required handler session. Throws InvalidOperationException for an anonymous connection or incompatible typed session.</summary>
    protected new TSession Session => base.Session as TSession
        ?? throw new InvalidOperationException($"Expected session type {typeof(TSession).FullName}");
}
