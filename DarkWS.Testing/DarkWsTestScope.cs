using Microsoft.Extensions.DependencyInjection;

namespace DarkWS.Testing;

/// <summary>A message scope for direct handler unit tests. It bypasses registration, authorization, initializers, and filters.</summary>
public sealed class DarkWsTestScope : IAsyncDisposable {
    private readonly AsyncServiceScope _scope;
    private readonly DarkWsContextAccessor _context;
    private bool _disposed;

    internal DarkWsTestScope(AsyncServiceScope scope, DarkWsTestConnection connection, CancellationToken cancellationToken) {
        _scope = scope;
        _context = scope.ServiceProvider.GetRequiredService<DarkWsContextAccessor>();
        _context.Initialize(connection, cancellationToken, connection.Session, scope.ServiceProvider,
            scope.ServiceProvider.GetRequiredService<Abstractions.IBroadcaster>());
    }

    /// <summary>Gets the scoped provider for resolving handlers and their dependencies.</summary>
    public IServiceProvider Services => _scope.ServiceProvider;

    /// <summary>Attaches this scope's context to a handler created by the test or resolved from Services.</summary>
    public void Initialize(HandlerBase handler) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(handler);
        handler.Initialize(_context);
    }

    /// <summary>Disposes scoped dependencies asynchronously. Do not use initialized handlers afterwards.</summary>
    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }
        _disposed = true;
        await _scope.DisposeAsync();
    }
}
