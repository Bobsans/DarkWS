namespace DarkBoy.DarkWS.Abstractions;

public interface IDarkWsScopeInitializer {
    ValueTask InitializeAsync(
        IServiceProvider scopedServices,
        IDarkWsContextAccessor context,
        CancellationToken cancellationToken
    );
}
