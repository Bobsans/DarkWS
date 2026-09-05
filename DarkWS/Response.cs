using System.Text.Json;
using DarkWS.Abstractions;

namespace DarkWS;

public sealed class ResponseContext(
    IWebSocketConnection connection,
    string requestId,
    DarkWsOptions options
) {
    public string RequestId { get; } = requestId;

    public Task SendAsync<T>(T data, CancellationToken cancellationToken = default) {
        return connection.SendAsync(
            JsonSerializer.SerializeToUtf8Bytes(data, options.JsonOptions),
            cancellationToken
        );
    }
}

public interface IResponse {
    Task WriteResultAsync(ResponseContext context, CancellationToken cancellationToken = default);
}

public sealed class SuccessResponse<T>(T data) : IResponse {
    public Task WriteResultAsync(ResponseContext context, CancellationToken cancellationToken = default) {
        return context.SendAsync(new ResponseMessage<T>(context.RequestId, data), cancellationToken);
    }
}

public sealed class SuccessResponse : IResponse {
    public Task WriteResultAsync(ResponseContext context, CancellationToken cancellationToken = default) {
        return context.SendAsync(new OkMessage(context.RequestId), cancellationToken);
    }
}

public class ErrorResponse(string error) : IResponse {
    public Task WriteResultAsync(ResponseContext context, CancellationToken cancellationToken = default) {
        return context.SendAsync(new ErrorMessage(context.RequestId, error), cancellationToken);
    }
}

public sealed class ErrorResponse<T>(string error, T details) : IResponse {
    public Task WriteResultAsync(ResponseContext context, CancellationToken cancellationToken = default) {
        return context.SendAsync(new ErrorMessage<T>(context.RequestId, error, details), cancellationToken);
    }
}
