namespace DarkWS.Client;

/// <summary>Readiness of a logical client connection.</summary>
public enum DarkWsClientState {
    /// <summary>No usable connection.</summary>
    Disconnected,
    /// <summary>Opening the first connection of a cycle.</summary>
    Connecting,
    /// <summary>Socket and optional automatic authentication are ready.</summary>
    Connected,
    /// <summary>Recovering a transport connection.</summary>
    Reconnecting,
    /// <summary>The client has been permanently released.</summary>
    Disposed
}

/// <summary>An ordered connection state transition.</summary>
public sealed class DarkWsStateChangedEventArgs : EventArgs {
    /// <summary>Creates a state transition.</summary>
    public DarkWsStateChangedEventArgs(DarkWsClientState previousState, DarkWsClientState state, Exception? reason = null) {
        PreviousState = previousState;
        State = state;
        Reason = reason;
    }
    /// <summary>State before the transition.</summary>
    public DarkWsClientState PreviousState { get; }
    /// <summary>State after the transition.</summary>
    public DarkWsClientState State { get; }
    /// <summary>Optional transport or protocol failure.</summary>
    public Exception? Reason { get; }
}

/// <summary>A background or subscriber error.</summary>
public sealed class DarkWsClientErrorEventArgs : EventArgs {
    /// <summary>Creates an error notification.</summary>
    public DarkWsClientErrorEventArgs(Exception exception) => Exception = exception;
    /// <summary>The failure. Do not log server or application data without appropriate redaction.</summary>
    public Exception Exception { get; }
}
