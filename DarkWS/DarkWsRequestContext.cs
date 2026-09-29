using System.Text.Json;
using DarkWS.Abstractions;

namespace DarkWS;

/// <summary>One well-formed request, available to request filters before the action is looked up or bound.</summary>
public sealed class DarkWsRequestContext {
    internal DarkWsRequestContext(string requestId, string actionName, DarkWsActionInfo? action, JsonElement? rawPayload,
        IDarkWsSession? session, IServiceProvider services, CancellationToken cancellationToken) {
        RequestId = requestId;
        ActionName = actionName;
        Action = action;
        RawPayload = rawPayload;
        Session = session;
        Services = services;
        CancellationToken = cancellationToken;
    }

    /// <summary>Gets the client request id, which correlates the request with its response.</summary>
    public string RequestId { get; }

    /// <summary>Gets the action name sent by the client, registered or not.</summary>
    public string ActionName { get; }

    /// <summary>Gets the registered action metadata, or null for an unknown action.</summary>
    public DarkWsActionInfo? Action { get; }

    /// <summary>Gets the request's data field as received, or null when it was omitted.</summary>
    public JsonElement? RawPayload { get; }

    /// <summary>Gets the session captured for this request, or null for an anonymous request.</summary>
    public IDarkWsSession? Session { get; }

    /// <summary>Gets the current message scope's service provider.</summary>
    public IServiceProvider Services { get; }

    /// <summary>Gets the token signaled when the connection stops.</summary>
    public CancellationToken CancellationToken { get; }
}
