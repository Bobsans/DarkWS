namespace DarkWS.Client;

/// <summary>One logical connection and server session. Safe for concurrent callers.</summary>
public interface IDarkWsClient : IDisposable, IAsyncDisposable {
    /// <summary>Current connection readiness.</summary>
    DarkWsClientState State { get; }
    /// <summary>Ordered state transitions delivered outside the socket reader and internal locks.</summary>
    event EventHandler<DarkWsStateChangedEventArgs>? StateChanged;
    /// <summary>Background and subscriber errors. Request errors are returned through their tasks.</summary>
    event EventHandler<DarkWsClientErrorEventArgs>? Error;
    /// <summary>Starts or joins a connection cycle. Cancelling the wait does not stop other callers.</summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);
    /// <summary>Stops reconnect and closes the socket. Explicit ConnectAsync allows reuse.</summary>
    Task CloseAsync(CancellationToken cancellationToken = default);
    // These initial API overloads deliberately distinguish omitted payload from explicit null.
#pragma warning disable RS0026
    /// <summary>Sends a request without a payload and deserializes its required data field.</summary>
    Task<TResponse> RequestAsync<TResponse>(string action, CancellationToken cancellationToken = default);
    /// <summary>Sends a payload, including explicit null, and deserializes the required data field.</summary>
    Task<TResponse> RequestAsync<TResponse>(string action, object? payload, CancellationToken cancellationToken = default);
    /// <summary>Sends a request without a payload and waits for success, ignoring returned data.</summary>
    Task RequestAsync(string action, CancellationToken cancellationToken = default);
    /// <summary>Sends a payload and waits for success, ignoring returned data.</summary>
    Task RequestAsync(string action, object? payload, CancellationToken cancellationToken = default);
#pragma warning restore RS0026
    /// <summary>Waits for acknowledged authentication. The supplied token is not retained for reconnect.</summary>
    Task AuthenticateAsync(string token, CancellationToken cancellationToken = default);
    /// <summary>Disables automatic session authentication and waits for acknowledged logout.</summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);
    /// <summary>Subscribes to an action, ignoring its optional payload. Dispose to unsubscribe.</summary>
    IDisposable On(string action, Action handler);
    /// <summary>Subscribes to typed action data. Callbacks run off the UI context, in delivery order.</summary>
    IDisposable On<T>(string action, Action<T> handler);
    /// <summary>Subscribes to async typed callbacks. Cancellation signals loss of the originating connection.</summary>
    IDisposable OnAsync<T>(string action, Func<T, CancellationToken, Task> handler);
}
