namespace DarkWS.Abstractions;

/// <summary>Initializes scoped application services before each handler invocation.</summary>
public interface IDarkWsScopeInitializer {
    /// <summary>Initializes application services in the current message scope before handler invocation.</summary>
    ValueTask InitializeAsync(
        IServiceProvider scopedServices,
        IDarkWsContextAccessor context,
        CancellationToken cancellationToken
    );
}
