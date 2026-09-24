using System.Text.Json;
using DarkWS.Abstractions;

namespace DarkWS;

/// <summary>Serializes an action response and sends it to the requesting connection.</summary>
/// <param name="connection">Connection receiving the response.</param>
/// <param name="requestId">Correlation id from the request.</param>
/// <param name="options">Validated serialization and transport settings.</param>
public sealed class ResponseContext(
    IWebSocketConnection connection,
    string requestId,
    DarkWsOptions options
) {
    private readonly IWebSocketConnection _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    private readonly DarkWsOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    /// <summary>Gets the id used to correlate the response with its request.</summary>
    public string RequestId { get; } = requestId ?? throw new ArgumentNullException(nameof(requestId));

    /// <summary>Serializes data using configured JSON options and sends it to the requesting connection.</summary>
    public Task SendAsync<T>(T data, CancellationToken cancellationToken = default) {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, _options.JsonOptions);
        HasStarted = true;
        return _connection.SendAsync(bytes, cancellationToken);
    }

    // False until serialized bytes reach the connection, so an earlier failure can still be answered with an error.
    internal bool HasStarted { get; private set; }
}

/// <summary>Writes one action result using a correlation context and cancellation token.</summary>
public interface IResponse {
    /// <summary>Writes this result using the supplied correlation context and cancellation token.</summary>
    Task WriteResultAsync(ResponseContext context, CancellationToken cancellationToken = default);
}

/// <summary>Writes a successful response with optional typed result data.</summary>
/// <param name="data">Result or message data.</param>
public sealed class SuccessResponse<T>(T data) : IResponse {
    /// <summary>Writes this result using the supplied correlation context and cancellation token.</summary>
    public Task WriteResultAsync(ResponseContext context, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);
        return context.SendAsync(new ResponseMessage<T>(context.RequestId, data), cancellationToken);
    }
}

/// <summary>Writes a successful response with optional typed result data.</summary>
public sealed class SuccessResponse : IResponse {
    /// <summary>Writes this result using the supplied correlation context and cancellation token.</summary>
    public Task WriteResultAsync(ResponseContext context, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);
        return context.SendAsync(new OkMessage(context.RequestId), cancellationToken);
    }
}

/// <summary>Writes an error code and optional typed details to the client.</summary>
/// <param name="error">Stable error code.</param>
public class ErrorResponse(string error) : IResponse {
    private readonly string _error = ValidateError(error);
    /// <summary>Writes this result using the supplied correlation context and cancellation token.</summary>
    public Task WriteResultAsync(ResponseContext context, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);
        return context.SendAsync(new ErrorMessage(context.RequestId, _error), cancellationToken);
    }

    internal static string ValidateError(string error) {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return error;
    }
}

/// <summary>Writes an error code and optional typed details to the client.</summary>
/// <param name="error">Stable error code.</param>
/// <param name="details">Typed details sent to the client.</param>
public sealed class ErrorResponse<T>(string error, T details) : IResponse {
    private readonly string _error = ErrorResponse.ValidateError(error);
    /// <summary>Writes this result using the supplied correlation context and cancellation token.</summary>
    public Task WriteResultAsync(ResponseContext context, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);
        return context.SendAsync(new ErrorMessage<T>(context.RequestId, _error, details), cancellationToken);
    }
}
