using System.Text.Json;

namespace DarkBoy.DarkWS;

internal abstract class ActionDescriptorBase(
    Type handlerType,
    Type? parameterType,
    bool allowAnonymous
) {
    public Type HandlerType { get; } = handlerType;
    public bool AllowAnonymous { get; } = allowAnonymous;

    public object? DeserializeParameter(JsonElement? value, JsonSerializerOptions options) {
        return parameterType is null ? null : value?.Deserialize(parameterType, options);
    }

    public abstract ValueTask<IResponse> InvokeAsync(object handler, object? parameter);
}

internal sealed class ActionDescriptor(
    Type handlerType,
    Type? parameterType,
    bool allowAnonymous,
    Func<object, object?, IResponse> handler
) : ActionDescriptorBase(handlerType, parameterType, allowAnonymous) {
    public override ValueTask<IResponse> InvokeAsync(object instance, object? parameter) {
        return ValueTask.FromResult(handler(instance, parameter));
    }
}

internal sealed class AsyncActionDescriptor(
    Type handlerType,
    Type? parameterType,
    bool allowAnonymous,
    Func<object, object?, Task<IResponse>> handler
) : ActionDescriptorBase(handlerType, parameterType, allowAnonymous) {
    public override async ValueTask<IResponse> InvokeAsync(object instance, object? parameter) {
        return await handler(instance, parameter);
    }
}
