using System.Text.Json;
using DarkWS.Abstractions;

namespace DarkWS;

/// <summary>Current action invocation data available to action filters.</summary>
public sealed class DarkWsActionContext {
    internal DarkWsActionContext(DarkWsRequestContext request, DarkWsActionInfo action, object? payload) {
        RequestId = request.RequestId;
        RawPayload = request.RawPayload;
        Action = action;
        Payload = payload;
        Session = request.Session;
        Services = request.Services;
        CancellationToken = request.CancellationToken;
    }

    /// <summary>Gets the client request id, which correlates the request with its response.</summary>
    public string RequestId { get; }

    /// <summary>Gets the same action metadata exposed by the message context accessor.</summary>
    public DarkWsActionInfo Action { get; }

    /// <summary>Gets the deserialized action payload, or null when omitted.</summary>
    public object? Payload { get; }

    /// <summary>Gets the request's data field as received, or null when it was omitted.</summary>
    public JsonElement? RawPayload { get; }

    /// <summary>Gets the session captured for this action, or null for an anonymous request.</summary>
    public IDarkWsSession? Session { get; }

    /// <summary>Gets the current message scope's service provider.</summary>
    public IServiceProvider Services { get; }

    /// <summary>Gets the token signaled when the connection stops.</summary>
    public CancellationToken CancellationToken { get; }
}
