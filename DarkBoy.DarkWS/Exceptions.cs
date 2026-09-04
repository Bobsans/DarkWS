namespace DarkBoy.DarkWS;

public abstract class DarkWsException : Exception {
    public abstract IResponse GetResponse();
}

public class ErrorResponseException(string error) : DarkWsException {
    public override IResponse GetResponse() => new ErrorResponse(error);
}

public class ErrorResponseException<T>(string error, T details) : DarkWsException {
    public override IResponse GetResponse() => new ErrorResponse<T>(error, details);
}
