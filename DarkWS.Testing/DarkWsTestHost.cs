using System.Collections.Concurrent;
using System.Text.Json;
using DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DarkWS.Testing;

/// <summary>Runs registered actions through the server pipeline without opening sockets or starting hosted services.</summary>
public sealed class DarkWsTestHost : IAsyncDisposable {
    private readonly ServiceProvider _services;
    private readonly InMemoryDarkWsBackplane _backplane = new();
    private readonly ConcurrentQueue<DarkWsBroadcast> _broadcasts = new();
    private readonly List<DarkWsTestConnection> _connections = [];
    private bool _disposed;

    /// <summary>Registers DarkWS and invokes application configuration. Always uses an isolated in-memory backplane.</summary>
    public DarkWsTestHost(Action<DarkWsBuilder> configure, Action<DarkWsOptions>? configureOptions = null) {
        ArgumentNullException.ThrowIfNull(configure);
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services.AddDarkWs(configureOptions));
        services.Replace(ServiceDescriptor.Singleton<IDarkWsBackplane>(_backplane));
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _backplane.SubscribeAsync((message, cancellationToken) => {
            _broadcasts.Enqueue(message);
            return _services.GetRequiredService<Broadcaster>().DeliverAsync(message, cancellationToken);
        }).GetAwaiter().GetResult();
    }

    /// <summary>Gets the root provider for application singleton inspection. Resolve scoped dependencies through CreateScope.</summary>
    public IServiceProvider Services => _services;

    /// <summary>Gets all published broadcasts, including those with no matching recipient.</summary>
    public IReadOnlyList<DarkWsBroadcast> Broadcasts => _broadcasts.ToArray();

    /// <summary>Registers a fake recipient with a fixed session. Configure connections before invoking concurrent actions.</summary>
    public DarkWsTestConnection CreateConnection(IDarkWsSession? session = null, HttpContext? httpContext = null) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var connection = new DarkWsTestConnection(session, httpContext);
        connection.HttpContext.RequestServices = _services;
        _services.GetRequiredService<ConnectionStorage>().Add(connection);
        _connections.Add(connection);
        return connection;
    }

    /// <summary>Creates a scope for manually initialized handlers. Use InvokeAsync to exercise the registered action pipeline.</summary>
    public DarkWsTestScope CreateScope(DarkWsTestConnection connection, CancellationToken cancellationToken = default) {
        ValidateConnection(connection);
        return new DarkWsTestScope(_services.CreateAsyncScope(), connection, cancellationToken);
    }

    /// <summary>Invokes an action with JSON data through real request validation, authorization, binding, filters, and response writing.</summary>
    /// <remarks>Each invocation owns a fresh async message scope. Responses and delivered broadcasts appear in connection.SentMessages.
    /// Cancellation follows server behavior and can finish without a response. This does not simulate authentication, lifecycle hooks, or transport limits.</remarks>
    public async Task InvokeAsync(DarkWsTestConnection connection, string action, JsonElement? data = null,
        string? requestId = null, CancellationToken cancellationToken = default) {
        ValidateConnection(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = _services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<WebSocketHandler>();
        var options = _services.GetRequiredService<IOptions<DarkWsOptions>>().Value;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new InputMessage(requestId ?? Guid.NewGuid().ToString("N"), action, data), options.JsonOptions);
        var message = await dispatcher.ReadMessageAsync(bytes, connection, cancellationToken);
        if (message is not null) {
            await dispatcher.ProcessMessageAsync(message, connection, cancellationToken);
        }
    }

    private void ValidateConnection(DarkWsTestConnection connection) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(connection);
        if (!_connections.Contains(connection)) {
            throw new ArgumentException("Create the connection through this test host.", nameof(connection));
        }
        if (!connection.IsOpen) {
            throw new InvalidOperationException("The test connection is closed.");
        }
    }

    /// <summary>Stops broadcast delivery, closes fake connections, and disposes application services. Await active invocations first.</summary>
    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }
        _disposed = true;
        await _backplane.UnsubscribeAsync();
        foreach (var connection in _connections) {
            connection.Dispose();
        }
        await _services.DisposeAsync();
    }
}
