using Microsoft.AspNetCore.Http;

namespace DarkWS.Abstractions;

/// <summary>Current message context. Uninitialized access throws InvalidOperationException; lifecycle hooks receive context explicitly.</summary>
public interface IDarkWsContextAccessor {
    /// <summary>Gets the current session, or null for anonymous access.</summary>
    IDarkWsSession? Session { get; }
    /// <summary>Gets the HTTP upgrade context shared by the connection. Not thread-safe: concurrent actions of the connection share it.</summary>
    HttpContext HttpContext { get; }
    /// <summary>Gets ASP.NET session state when its middleware is installed, otherwise null. Not thread-safe: concurrent actions of the connection share it.</summary>
    ISession? AspNetSession { get; }
    /// <summary>Gets the current connection during initialized message handling.</summary>
    IWebSocketConnection Connection { get; }
    /// <summary>Gets the token signaled when the connection stops, or in OnCloseAsync when the shutdown deadline expires. Observe it during asynchronous work.</summary>
    CancellationToken ConnectionAborted { get; }
}
