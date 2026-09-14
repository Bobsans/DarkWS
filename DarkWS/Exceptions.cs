namespace DarkWS;

/// <summary>Base for controlled handler errors that supply their own protocol response.</summary>
public abstract class DarkWsException : Exception {
    /// <summary>Creates a controlled exception with the default exception message.</summary>
    protected DarkWsException() { }

    /// <summary>Creates a controlled exception carrying a non-empty error code and optional cause.</summary>
    protected DarkWsException(string error, Exception? innerException = null)
        : base(ErrorResponse.ValidateError(error), innerException) { }
    /// <summary>Returns the controlled protocol response associated with this exception.</summary>
    public abstract IResponse GetResponse();
}

/// <summary>Controlled handler exception translated to an error code and optional typed details.</summary>
public class ErrorResponseException : DarkWsException {
    private readonly string _error;

    /// <summary>Creates a controlled error carrying its wire code as the exception message.</summary>
    public ErrorResponseException(string error) : this(error, null) { }

    /// <summary>Creates a controlled error with its original cause.</summary>
    public ErrorResponseException(string error, Exception? innerException) : base(error, innerException) {
        _error = error;
    }
    /// <summary>Returns the controlled protocol response associated with this exception.</summary>
    public override IResponse GetResponse() => new ErrorResponse(_error);
}

/// <summary>Controlled handler exception translated to an error code and optional typed details.</summary>
public class ErrorResponseException<T> : DarkWsException {
    private readonly string _error;
    private readonly T _details;

    /// <summary>Creates a controlled error with typed details sent to the client.</summary>
    public ErrorResponseException(string error, T details) : this(error, details, null) { }

    /// <summary>Creates a controlled error with typed client details and its original cause.</summary>
    public ErrorResponseException(string error, T details, Exception? innerException) : base(error, innerException) {
        _error = error;
        _details = details;
    }
    /// <summary>Returns the controlled protocol response associated with this exception.</summary>
    public override IResponse GetResponse() => new ErrorResponse<T>(_error, _details);
}
