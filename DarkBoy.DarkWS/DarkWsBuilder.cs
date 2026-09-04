using System.Reflection;
using DarkBoy.DarkWS.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DarkBoy.DarkWS;

public sealed class DarkWsBuilder {
    private readonly DarkWsActionRegistry _registry;

    internal DarkWsBuilder(IServiceCollection services, DarkWsActionRegistry registry) {
        Services = services;
        _registry = registry;
    }

    public IServiceCollection Services { get; }

    public DarkWsBuilder AddHandlersFromAssemblyContaining<T>() {
        return AddHandlersFromAssembly(typeof(T).Assembly);
    }

    public DarkWsBuilder AddHandlersFromAssembly(Assembly assembly) {
        foreach (var type in GetLoadableTypes(assembly).Where(it => !it.IsAbstract)) {
            if (type.IsSubclassOf(typeof(HandlerBase))) {
                Services.AddScoped(type);
                _registry.Add(type);
            }

            if (type.IsSubclassOf(typeof(DarkWsMiddleware))) {
                Services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(DarkWsMiddleware), type));
            }
        }

        return this;
    }

    public DarkWsBuilder AddAuthenticator<TAuthenticator, TSession>()
        where TAuthenticator : class, IDarkWsAuthenticator
        where TSession : class, IDarkWsSession {
        Services.Replace(ServiceDescriptor.Scoped<IDarkWsAuthenticator, TAuthenticator>());
        Services.AddScoped(provider => provider.GetRequiredService<IDarkWsContextAccessor>().Session as TSession
            ?? throw new InvalidOperationException($"Expected session type {typeof(TSession).FullName}"));
        return this;
    }

    public DarkWsBuilder AddScopeInitializer<TInitializer>()
        where TInitializer : class, IDarkWsScopeInitializer {
        Services.AddScoped<IDarkWsScopeInitializer, TInitializer>();
        return this;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly) {
        try {
            return assembly.GetTypes();
        } catch (ReflectionTypeLoadException error) {
            return error.Types.OfType<Type>();
        }
    }
}
