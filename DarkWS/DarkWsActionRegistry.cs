using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;

namespace DarkWS;

internal sealed class DarkWsActionRegistry {
    private readonly Dictionary<string, ActionDescriptorBase> _actions = new(StringComparer.Ordinal);

    public bool TryGet(string action, out ActionDescriptorBase descriptor) {
        return _actions.TryGetValue(action, out descriptor!);
    }

    public void Add(Type type) {
        var handlerAttributes = Array.AsReadOnly(type.GetCustomAttributes(inherit: true).OfType<Attribute>().ToArray());
        if (handlerAttributes.OfType<IAuthorizeData>().Any()) {
            throw new InvalidOperationException($"Handler '{type.FullName}' uses unsupported [Authorize] metadata. DarkWS supports only [AllowAnonymous]; enforce policies and roles inside actions");
        }

        var handler = type.GetCustomAttribute<HandlerAttribute>();
        if (handler is not null && !IsValidName(handler.Name)) {
            throw new InvalidOperationException($"Handler '{type.FullName}' has invalid name '{handler.Name}'; names must be non-empty without surrounding whitespace");
        }

        var handlerName = handler?.Name;
        var classAllowsAnonymous = type.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
            var action = method.GetCustomAttribute<ActionAttribute>();
            if (action is null) {
                continue;
            }

            if (method.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().Any()) {
                throw new InvalidOperationException($"Action method '{type.FullName}.{method.Name}' uses unsupported [Authorize] metadata. DarkWS supports only [AllowAnonymous]; enforce policies and roles inside actions");
            }

            var parameters = method.GetParameters();
            var payloads = parameters.Count(parameter => parameter.ParameterType != typeof(CancellationToken));
            string? reason = !method.IsPublic || method.IsStatic ? "must be a public instance method"
                : method.ContainsGenericParameters ? "must not have unbound generic parameters"
                : payloads > 1 || parameters.Length - payloads > 1 ? "must have zero or one payload parameter and at most one CancellationToken"
                : parameters.Any(parameter => parameter.ParameterType.IsByRef || parameter.ParameterType.IsByRefLike || parameter.ParameterType.IsPointer || parameter.ParameterType.IsFunctionPointer) ? "payload must not be by-reference, byref-like, or a pointer"
                : method.ReturnType != typeof(IResponse) && method.ReturnType != typeof(Task<IResponse>) ? "must return IResponse or Task<IResponse>"
                : !IsValidName(action.Name) ? $"has invalid action name '{action.Name}'; names must be non-empty without surrounding whitespace"
                : null;
            if (reason is not null) {
                throw new InvalidOperationException($"Action method '{type.FullName}.{method.Name}' {reason}");
            }

            var key = handlerName is null ? action.Name : $"{handlerName}:{action.Name}";
            if (_actions.ContainsKey(key)) {
                throw new InvalidOperationException($"Handler action '{key}' is already registered");
            }

            var allowAnonymous = classAllowsAnonymous || method.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
            _actions.Add(key, CreateDescriptor(new DarkWsActionInfo(key, type, method, handlerAttributes), allowAnonymous));
        }
    }

    // A blank action is rejected as an invalid request, so it could never be reached; padding is almost always a typo.
    private static bool IsValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim() == name;

    private static ActionDescriptorBase CreateDescriptor(DarkWsActionInfo action, bool allowAnonymous) {
        var instance = Expression.Parameter(typeof(object), "instance");
        var parameter = Expression.Parameter(typeof(object), "parameter");
        var cancellationToken = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        var parameters = action.Method.GetParameters();
        // The payload and the token may come in either order.
        var arguments = parameters.Select(it => it.ParameterType == typeof(CancellationToken)
            ? (Expression)cancellationToken
            : Expression.Convert(parameter, it.ParameterType));
        var call = Expression.Call(Expression.Convert(instance, action.HandlerType), action.Method, arguments);
        var payload = parameters.FirstOrDefault(it => it.ParameterType != typeof(CancellationToken));
        var parameterType = payload?.ParameterType;
        var allowsNullPayload = payload is null || (parameterType!.IsValueType
            ? Nullable.GetUnderlyingType(parameterType) is not null
            : new NullabilityInfoContext().Create(payload).ReadState != NullabilityState.NotNull);

        if (action.Method.ReturnType == typeof(IResponse)) {
            var invoker = Expression.Lambda<Func<object, object?, CancellationToken, IResponse>>(call, instance, parameter, cancellationToken).Compile();
            return new ActionDescriptor(action, parameterType, allowAnonymous, allowsNullPayload, invoker);
        }

        var asyncInvoker = Expression.Lambda<Func<object, object?, CancellationToken, Task<IResponse>>>(call, instance, parameter, cancellationToken).Compile();
        return new AsyncActionDescriptor(action, parameterType, allowAnonymous, allowsNullPayload, asyncInvoker);
    }
}
