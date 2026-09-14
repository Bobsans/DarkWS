using System.Text.Json;

namespace DarkWS;

internal abstract class ActionDescriptorBase(
    Type handlerType,
    Type? parameterType,
    bool allowAnonymous,
    bool allowsNullPayload
) {
    public Type HandlerType { get; } = handlerType;
    public bool AllowAnonymous { get; } = allowAnonymous;

    public object? DeserializeParameter(JsonElement? value, JsonSerializerOptions options) {
        if (parameterType is null) return null;
        var parameter = value?.Deserialize(parameterType, options);
        if (parameter is null && !allowsNullPayload) {
            throw new JsonException("A non-null payload is required");
        }
        return parameter;
    }

    public abstract ValueTask<IResponse> InvokeAsync(object handler, object? parameter);
}

internal sealed class ActionDescriptor(
    Type handlerType,
    Type? parameterType,
    bool allowAnonymous,
    bool allowsNullPayload,
    Func<object, object?, IResponse> handler
) : ActionDescriptorBase(handlerType, parameterType, allowAnonymous, allowsNullPayload) {
    public override ValueTask<IResponse> InvokeAsync(object instance, object? parameter) {
        return ValueTask.FromResult(handler(instance, parameter));
    }
}

internal sealed class AsyncActionDescriptor(
    Type handlerType,
    Type? parameterType,
    bool allowAnonymous,
    bool allowsNullPayload,
    Func<object, object?, Task<IResponse>> handler
) : ActionDescriptorBase(handlerType, parameterType, allowAnonymous, allowsNullPayload) {
    public override async ValueTask<IResponse> InvokeAsync(object instance, object? parameter) {
        return await handler(instance, parameter);
    }
}
