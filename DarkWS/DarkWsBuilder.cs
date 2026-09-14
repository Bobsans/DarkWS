using System.Reflection;
using DarkWS.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DarkWS;

/// <summary>Composes handlers, authentication, and message-scope services after a single AddDarkWs call.</summary>
public sealed class DarkWsBuilder {
    private readonly DarkWsActionRegistry _registry;

    internal DarkWsBuilder(IServiceCollection services, DarkWsActionRegistry registry) {
        Services = services;
        _registry = registry;
    }

    /// <summary>Gets the service collection being configured.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Scans the marker type's assembly for concrete handlers and middleware; invalid or duplicate actions fail registration.</summary>
    public DarkWsBuilder AddHandlersFromAssemblyContaining<T>() {
        return AddHandlersFromAssembly(typeof(T).Assembly);
    }

    /// <summary>Scans concrete handlers and middleware; only actions declared on the concrete handler are registered.</summary>
    public DarkWsBuilder AddHandlersFromAssembly(Assembly assembly) {
        ArgumentNullException.ThrowIfNull(assembly);
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

    /// <summary>Registers the authenticator and typed session injection in message scopes. Missing or incompatible sessions throw InvalidOperationException.</summary>
    public DarkWsBuilder AddAuthenticator<TAuthenticator, TSession>()
        where TAuthenticator : class, IDarkWsAuthenticator
        where TSession : class, IDarkWsSession {
        Services.Replace(ServiceDescriptor.Scoped<IDarkWsAuthenticator, TAuthenticator>());
        Services.AddScoped(provider => provider.GetRequiredService<IDarkWsContextAccessor>().Session as TSession
            ?? throw new InvalidOperationException($"Expected session type {typeof(TSession).FullName}"));
        return this;
    }

    /// <summary>Registers initialization of scoped services before every handler invocation.</summary>
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
