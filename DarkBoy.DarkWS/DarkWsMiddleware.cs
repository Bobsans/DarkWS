using DarkBoy.DarkWS.Abstractions;

namespace DarkBoy.DarkWS;

public abstract class DarkWsMiddleware {
    public virtual Task OnOpenAsync(IDarkWsContextAccessor context) => Task.CompletedTask;

    public virtual Task OnAuthenticatedAsync(
        IDarkWsContextAccessor context,
        IDarkWsSession? previousSession
    ) => Task.CompletedTask;

    public virtual Task OnCloseAsync(IDarkWsContextAccessor context) => Task.CompletedTask;
}
