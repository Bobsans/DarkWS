using System.Net.WebSockets;
using System.Text.Json;

namespace DarkWS.Client;

/// <summary>A controlled server error. Payloads are excluded from the exception message.</summary>
public sealed class DarkWsResponseException : Exception {
    /// <summary>Creates a correlated server error with independently owned JSON data.</summary>
    public DarkWsResponseException(string code, JsonElement? errorData, string requestId, string action)
        : base("The server rejected the DarkWS request.") {
        Code = code;
        ErrorData = errorData?.Clone();
        RequestId = requestId;
        Action = action;
    }
    /// <summary>Server error code, possibly empty.</summary>
    public string Code { get; }
    /// <summary>Optional server error data, independent of receive buffers.</summary>
    public JsonElement? ErrorData { get; }
    /// <summary>Correlated request identifier, or empty for a text system command.</summary>
    public string RequestId { get; }
    /// <summary>Requested action.</summary>
    public string Action { get; }
}

/// <summary>A socket or connection lifecycle failure.</summary>
public sealed class DarkWsConnectionException : Exception {
    /// <summary>Creates a connection failure. Avoid sensitive content in the supplied message or cause.</summary>
    public DarkWsConnectionException(string message, WebSocketCloseStatus? closeStatus = null, string? closeReason = null, Exception? innerException = null)
        : base(message, innerException) {
        CloseStatus = closeStatus;
        CloseReason = closeReason;
    }
    /// <summary>Peer close status, when available.</summary>
    public WebSocketCloseStatus? CloseStatus { get; }
    /// <summary>Peer-provided close reason. May contain application data; excluded from Message.</summary>
    public string? CloseReason { get; }
}

/// <summary>The stage that exceeded its timeout.</summary>
public enum DarkWsTimeoutStage {
    /// <summary>Waiting for a ready connection.</summary>
    Connection,
    /// <summary>Waiting for the send gate or writing to the socket.</summary>
    Send,
    /// <summary>Waiting for a response after a successful send.</summary>
    Response
}

/// <summary>A bounded client operation expired.</summary>
public sealed class DarkWsTimeoutException : TimeoutException {
    /// <summary>Creates a timeout for the given stage.</summary>
    public DarkWsTimeoutException(DarkWsTimeoutStage stage) : base($"The DarkWS {stage} operation timed out.") => Stage = stage;
    /// <summary>The expired stage.</summary>
    public DarkWsTimeoutStage Stage { get; }
}

/// <summary>A malformed or unsupported incoming message.</summary>
public sealed class DarkWsProtocolException : Exception {
    /// <summary>Creates a protocol error with the close status used for fatal wire violations.</summary>
    public DarkWsProtocolException(string message, WebSocketCloseStatus closeStatus = WebSocketCloseStatus.ProtocolError) : base(message) => CloseStatus = closeStatus;
    /// <summary>Close status used when the violation affects the connection.</summary>
    public WebSocketCloseStatus CloseStatus { get; }
}

/// <summary>A bounded local request or notification queue is full.</summary>
public sealed class DarkWsClientLimitException : Exception {
    /// <summary>Creates a client capacity error.</summary>
    public DarkWsClientLimitException(string message) : base(message) { }
}
