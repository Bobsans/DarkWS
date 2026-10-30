using DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DarkWS;

/// <summary>Base for per-message handlers. Context-dependent members are available only during action invocation.</summary>
public abstract class HandlerBase {
    private DarkWsContextAccessor Context { get; set; } = null!;

    /// <summary>Gets the current message scope's service provider.</summary>
    protected IServiceProvider Services => Context.Services;

    /// <summary>Gets the required handler session. Without one, throws ErrorResponseException with DarkWsOptions.AuthorizationRequiredError, which the client receives as that error.</summary>
    protected IDarkWsSession Session => Context.Session
        ?? throw new ErrorResponseException(Services.GetRequiredService<IOptions<DarkWsOptions>>().Value.AuthorizationRequiredError);

    /// <summary>Gets the session the action was authorized with, or null for an anonymous caller of an [AllowAnonymous] action.</summary>
    protected IDarkWsSession? SessionOrDefault => Context.Session;

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

    /// <summary>Gets a target selecting only the current handler connection.</summary>
    protected BroadcastTarget Self => BroadcastTarget.Connection(Connection.Id);

    /// <summary>Publishes an action without data to the selected recipients; use ExceptConnection(Connection.Id) to skip this handler's connection.</summary>
    protected Task PublishAsync(BroadcastTarget target, string action) {
        return Context.Broadcaster.PublishAsync(target, action, CancellationToken.None);
    }

    /// <summary>Publishes an action without data to the selected recipients; use ExceptConnection(Connection.Id) to skip this handler's connection.</summary>
    protected Task PublishAsync(BroadcastTarget target, string action, CancellationToken cancellationToken) {
        return Context.Broadcaster.PublishAsync(target, action, cancellationToken);
    }

    /// <summary>Publishes an action and data, including explicit null, to the selected recipients.</summary>
    protected Task PublishAsync<T>(BroadcastTarget target, string action, T? data) {
        return Context.Broadcaster.PublishAsync(target, action, data, CancellationToken.None);
    }

    /// <summary>Publishes an action and data, including explicit null, to the selected recipients.</summary>
    protected Task PublishAsync<T>(BroadcastTarget target, string action, T? data, CancellationToken cancellationToken) {
        return Context.Broadcaster.PublishAsync(target, action, data, cancellationToken);
    }
}

/// <summary>Base for per-message handlers. Context-dependent members are available only during action invocation.</summary>
public abstract class HandlerBase<TSession> : HandlerBase
    where TSession : class, IDarkWsSession {
    /// <summary>Gets the required handler session. Without one, throws ErrorResponseException with DarkWsOptions.AuthorizationRequiredError; an incompatible session type throws InvalidOperationException.</summary>
    protected new TSession Session => Cast(base.Session);

    /// <summary>Gets the typed session, or null for an anonymous caller. An incompatible session type throws InvalidOperationException.</summary>
    protected new TSession? SessionOrDefault => base.SessionOrDefault is { } session ? Cast(session) : null;

    private static TSession Cast(IDarkWsSession session) {
        return session as TSession ?? throw new InvalidOperationException($"Expected session type {typeof(TSession).FullName}");
    }
}
