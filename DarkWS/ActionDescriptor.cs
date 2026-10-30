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

    public object? DeserializeParameter(JsonElement? value, JsonSerializerOptions options, bool allowNullPayloads) {
        if (parameterType is null) {
            return null;
        }

        var parameter = value?.Deserialize(parameterType, options);
        if (parameter is null && !allowsNullPayload && (!allowNullPayloads || parameterType.IsValueType)) {
            throw new JsonException("A non-null payload is required");
        }

        return parameter;
    }

    public abstract ValueTask<IResponse> InvokeAsync(object handler, object? parameter, CancellationToken cancellationToken);
}

internal sealed class ActionDescriptor(
    DarkWsActionInfo action,
    Type? parameterType,
    bool allowAnonymous,
    bool allowsNullPayload,
    Func<object, object?, CancellationToken, IResponse> handler
) : ActionDescriptorBase(action, parameterType, allowAnonymous, allowsNullPayload) {
    public override ValueTask<IResponse> InvokeAsync(object instance, object? parameter, CancellationToken cancellationToken) {
        return ValueTask.FromResult(handler(instance, parameter, cancellationToken));
    }
}

internal sealed class AsyncActionDescriptor(
    DarkWsActionInfo action,
    Type? parameterType,
    bool allowAnonymous,
    bool allowsNullPayload,
    Func<object, object?, CancellationToken, Task<IResponse>> handler
) : ActionDescriptorBase(action, parameterType, allowAnonymous, allowsNullPayload) {
    public override async ValueTask<IResponse> InvokeAsync(object instance, object? parameter, CancellationToken cancellationToken) {
        return await handler(instance, parameter, cancellationToken);
    }
}
