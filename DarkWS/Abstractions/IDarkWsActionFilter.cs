namespace DarkWS.Abstractions;

/// <summary>Wraps a bound action call and may return a response without calling next.</summary>
public interface IDarkWsActionFilter {
    /// <summary>Invokes the next filter or handler. Call next at most once.</summary>
    ValueTask<IResponse> InvokeAsync(DarkWsActionContext context, Func<ValueTask<IResponse>> next);
}
