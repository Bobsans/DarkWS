namespace DarkWS.Abstractions;

/// <summary>Wraps every well-formed request in its message scope: action lookup, authorization, payload binding, scope initializers, action filters, and the handler.</summary>
/// <remarks>Unknown actions, missing authorization, and invalid payloads arrive as ErrorResponse results; failures of scope initializers, action filters, and handlers arrive as exceptions. Malformed JSON and busy rejections are answered before a message scope exists and do not reach request filters.</remarks>
public interface IDarkWsRequestFilter {
    /// <summary>Invokes the next filter or the request pipeline. Call next at most once.</summary>
    ValueTask<IResponse> InvokeAsync(DarkWsRequestContext context, Func<ValueTask<IResponse>> next);
}
