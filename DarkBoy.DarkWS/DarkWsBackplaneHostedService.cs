using DarkBoy.DarkWS.Abstractions;
using Microsoft.Extensions.Hosting;

namespace DarkBoy.DarkWS;

internal sealed class DarkWsBackplaneHostedService(
    IDarkWsBackplane backplane,
    Broadcaster broadcaster
) : IHostedService {
    public Task StartAsync(CancellationToken cancellationToken) {
        return backplane.SubscribeAsync(broadcaster.DeliverAsync, cancellationToken).AsTask();
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        return backplane.UnsubscribeAsync(cancellationToken).AsTask();
    }
}
