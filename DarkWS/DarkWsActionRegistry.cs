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
        if (type.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().Any()) {
            throw new InvalidOperationException($"Handler '{type.FullName}' uses unsupported [Authorize] metadata. DarkWS supports only [AllowAnonymous]; enforce policies and roles inside actions");
        }
        var handlerName = type.GetCustomAttribute<HandlerAttribute>()?.Name;
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
            string? reason = !method.IsPublic || method.IsStatic ? "must be a public instance method"
                : method.ContainsGenericParameters ? "must not have unbound generic parameters"
                : parameters.Length > 1 ? "must have zero or one payload parameter"
                : parameters.Any(parameter => parameter.ParameterType.IsByRef || parameter.ParameterType.IsByRefLike || parameter.ParameterType.IsPointer || parameter.ParameterType.IsFunctionPointer) ? "payload must not be by-reference, byref-like, or a pointer"
                : method.ReturnType != typeof(IResponse) && method.ReturnType != typeof(Task<IResponse>) ? "must return IResponse or Task<IResponse>"
                : null;
            if (reason is not null) {
                throw new InvalidOperationException($"Action method '{type.FullName}.{method.Name}' {reason}");
            }

            var key = handlerName is null ? action.Name : $"{handlerName}:{action.Name}";
            if (_actions.ContainsKey(key)) {
                throw new InvalidOperationException($"Handler action '{key}' is already registered");
            }

            var allowAnonymous = classAllowsAnonymous || method.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
            _actions.Add(key, CreateDescriptor(type, method, parameters.FirstOrDefault()?.ParameterType, allowAnonymous));
        }
    }

    private static ActionDescriptorBase CreateDescriptor(
        Type handlerType,
        MethodInfo method,
        Type? parameterType,
        bool allowAnonymous
    ) {
        var instance = Expression.Parameter(typeof(object), "instance");
        var parameter = Expression.Parameter(typeof(object), "parameter");
        var arguments = parameterType is null ? [] : new[] { Expression.Convert(parameter, parameterType) };
        var call = Expression.Call(Expression.Convert(instance, handlerType), method, arguments);
        var payload = method.GetParameters().FirstOrDefault();
        var allowsNullPayload = payload is null || (parameterType!.IsValueType
            ? Nullable.GetUnderlyingType(parameterType) is not null
            : new NullabilityInfoContext().Create(payload).ReadState != NullabilityState.NotNull);

        if (method.ReturnType == typeof(IResponse)) {
            var invoker = Expression.Lambda<Func<object, object?, IResponse>>(call, instance, parameter).Compile();
            return new ActionDescriptor(handlerType, parameterType, allowAnonymous, allowsNullPayload, invoker);
        }

        var asyncInvoker = Expression.Lambda<Func<object, object?, Task<IResponse>>>(call, instance, parameter).Compile();
        return new AsyncActionDescriptor(handlerType, parameterType, allowAnonymous, allowsNullPayload, asyncInvoker);
    }
}
