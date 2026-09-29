using System.Text.Json;

namespace DarkWS;

internal abstract class ActionDescriptorBase(
    DarkWsActionInfo action,
    Type? parameterType,
    bool allowAnonymous,
    bool allowsNullPayload
) {
    public DarkWsActionInfo Action { get; } = action;
    public Type HandlerType => Action.HandlerType;
    public bool AllowAnonymous { get; } = allowAnonymous;

    public object? DeserializeParameter(JsonElement? value, JsonSerializerOptions options) {
        if (parameterType is null) {
            return null;
        }

        var parameter = value?.Deserialize(parameterType, options);
        if (parameter is null && !allowsNullPayload) {
            throw new JsonException("A non-null payload is required");
        }

        return parameter;
    }

    public abstract ValueTask<IResponse> InvokeAsync(object handler, object? parameter);
}

internal sealed class ActionDescriptor(
    DarkWsActionInfo action,
    Type? parameterType,
    bool allowAnonymous,
    bool allowsNullPayload,
    Func<object, object?, IResponse> handler
) : ActionDescriptorBase(action, parameterType, allowAnonymous, allowsNullPayload) {
    public override ValueTask<IResponse> InvokeAsync(object instance, object? parameter) {
        return ValueTask.FromResult(handler(instance, parameter));
    }
}

internal sealed class AsyncActionDescriptor(
    DarkWsActionInfo action,
    Type? parameterType,
    bool allowAnonymous,
    bool allowsNullPayload,
    Func<object, object?, Task<IResponse>> handler
) : ActionDescriptorBase(action, parameterType, allowAnonymous, allowsNullPayload) {
    public override async ValueTask<IResponse> InvokeAsync(object instance, object? parameter) {
        return await handler(instance, parameter);
    }
}
