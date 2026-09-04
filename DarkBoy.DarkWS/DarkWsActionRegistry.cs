using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;

namespace DarkBoy.DarkWS;

internal sealed class DarkWsActionRegistry {
    private readonly Dictionary<string, ActionDescriptorBase> _actions = new(StringComparer.Ordinal);

    public bool TryGet(string action, out ActionDescriptorBase descriptor) {
        return _actions.TryGetValue(action, out descriptor!);
    }

    public void Add(Type type) {
        var handlerName = type.GetCustomAttribute<HandlerAttribute>()?.Name;
        var classAllowsAnonymous = type.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)) {
            var action = method.GetCustomAttribute<ActionAttribute>();
            var parameters = method.GetParameters();
            if (action is null || parameters.Length > 1 ||
                method.ReturnType != typeof(IResponse) && method.ReturnType != typeof(Task<IResponse>)) {
                continue;
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

        if (method.ReturnType == typeof(IResponse)) {
            var invoker = Expression.Lambda<Func<object, object?, IResponse>>(call, instance, parameter).Compile();
            return new ActionDescriptor(handlerType, parameterType, allowAnonymous, invoker);
        }

        var asyncInvoker = Expression.Lambda<Func<object, object?, Task<IResponse>>>(call, instance, parameter).Compile();
        return new AsyncActionDescriptor(handlerType, parameterType, allowAnonymous, asyncInvoker);
    }
}
