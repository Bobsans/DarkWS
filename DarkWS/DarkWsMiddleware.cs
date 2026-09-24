using DarkWS.Abstractions;

namespace DarkWS;

/// <summary>Connection lifecycle hooks. Use the supplied context; an injected message context is uninitialized in this scope.</summary>
public abstract class DarkWsMiddleware {
    /// <summary>Runs after connection registration with an initialized context.</summary>
    public virtual Task OnOpenAsync(IDarkWsContextAccessor context) => Task.CompletedTask;

    /// <summary>Runs after session replacement, failed re-authentication, or logout. The current session can be null; previousSession identifies the replaced session.</summary>
    public virtual Task OnAuthenticatedAsync(
        IDarkWsContextAccessor context,
        IDarkWsSession? previousSession
    ) => Task.CompletedTask;

    /// <summary>Runs after removal from storage. The context's ConnectionAborted is the ShutdownTimeout deadline shared with the close handshake; finish promptly.</summary>
    public virtual Task OnCloseAsync(IDarkWsContextAccessor context) => Task.CompletedTask;
}
