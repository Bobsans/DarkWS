using System.Text.Json;
using System.Threading.Channels;
using DarkWS.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DarkWS.Redis;

internal sealed class RedisDarkWsBackplane(
    IConnectionMultiplexer connection,
    RedisDarkWsOptions redisOptions,
    ILogger<RedisDarkWsBackplane> logger
) : IDarkWsBackplane {
    private const int ConcurrentDeliveries = 16;
    private readonly RedisChannel _channel = RedisChannel.Literal(redisOptions.Channel);
    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();
    private readonly SemaphoreSlim _subscriptionLock = new(1, 1);
    private ChannelMessageQueue? _subscription;
    private CancellationTokenSource? _subscriptionCancellation;
    private Task? _subscriptionDeliveries;

    public async ValueTask PublishAsync(
        DarkWsBroadcast message,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        // UTF-8 bytes go to Redis as they are, without a UTF-16 string in between.
        var json = JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions);
        await connection.GetSubscriber().PublishAsync(_channel, json);
    }

    public async ValueTask SubscribeAsync(
        Func<DarkWsBroadcast, CancellationToken, ValueTask> listener,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(listener);
        cancellationToken.ThrowIfCancellationRequested();
        await _subscriptionLock.WaitAsync(cancellationToken);
        try {
            if (_subscription is not null) {
                throw new InvalidOperationException("The Redis backplane already has a subscriber; unsubscribe before subscribing again");
            }

            var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try {
                var subscription = await connection.GetSubscriber().SubscribeAsync(_channel);
                var token = lifetime.Token;
                _subscriptionDeliveries = Task.WhenAll(Enumerable.Range(0, ConcurrentDeliveries)
                    .Select(_ => Task.Run(() => ReceiveAsync(subscription, listener, token))));
                _subscriptionCancellation = lifetime;
                _subscription = subscription;
            } catch {
                lifetime.Dispose();
                throw;
            }
        } finally {
            _subscriptionLock.Release();
        }
    }

    public async ValueTask UnsubscribeAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        await _subscriptionLock.WaitAsync(cancellationToken);
        try {
            if (_subscription is null) {
                return;
            }

            await _subscriptionCancellation!.CancelAsync();
            await _subscription.UnsubscribeAsync();
            _ = DisposeWhenCompletedAsync(_subscriptionDeliveries!, _subscriptionCancellation);
            _subscription = null;
            _subscriptionCancellation = null;
            _subscriptionDeliveries = null;
        } finally {
            _subscriptionLock.Release();
        }
    }

    private async Task ReceiveAsync(ChannelMessageQueue subscription, Func<DarkWsBroadcast, CancellationToken, ValueTask> listener, CancellationToken token) {
        try {
            while (true) {
                // Keep draining after lifetime cancellation until unsubscribe completes the Redis queue.
                var message = await subscription.ReadAsync();
                await HandleMessageAsync(message, listener, token);
            }
        } catch (ChannelClosedException) {
            // Unsubscribe completed and the remaining messages were drained.
        }
    }

    private async Task DisposeWhenCompletedAsync(Task deliveries, CancellationTokenSource lifetime) {
        try {
            await deliveries;
        } catch (Exception error) {
            logger.LogWarning(error, "Redis broadcast receivers failed on {Channel}", _channel);
        } finally {
            lifetime.Dispose();
        }
    }

    private async Task HandleMessageAsync(ChannelMessage message, Func<DarkWsBroadcast, CancellationToken, ValueTask> listener, CancellationToken cancellationToken) {
        try {
            cancellationToken.ThrowIfCancellationRequested();
            var broadcast = JsonSerializer.Deserialize<DarkWsBroadcast>((byte[])message.Message!, _jsonOptions);
            if (broadcast is not null) {
                await listener(broadcast, cancellationToken);
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            logger.LogDebug("Redis broadcast delivery cancelled on {Channel}", _channel);
        } catch (Exception error) {
            logger.LogWarning(error, "Invalid DarkWS Redis message on {Channel}", _channel);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
