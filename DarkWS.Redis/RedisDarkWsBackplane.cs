using System.Diagnostics.Metrics;
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
    private static readonly Meter _meter = new("DarkWS.Redis");
    private static readonly Counter<long> _received = _meter.CreateCounter<long>("darkws.redis.received", "{message}");
    private static readonly Counter<long> _dropped = _meter.CreateCounter<long>("darkws.redis.dropped", "{message}");
    private static readonly Counter<long> _rejected = _meter.CreateCounter<long>("darkws.redis.rejected", "{message}");
    private readonly RedisChannel _channel = RedisChannel.Literal(redisOptions.Channel);
    private readonly KeyValuePair<string, object?> _channelTag = new("channel", redisOptions.Channel);
    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();
    private readonly SemaphoreSlim _subscriptionLock = new(1, 1);
    private Action<RedisChannel, RedisValue>? _subscription;
    private Channel<RedisValue>? _subscriptionQueue;
    private CancellationTokenSource? _subscriptionCancellation;
    private Task? _subscriptionDeliveries;

    public async ValueTask PublishAsync(
        BroadcastMessage message,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        // UTF-8 bytes go to Redis as they are, without a UTF-16 string in between.
        var json = JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions);
        if (json.Length > redisOptions.MaxMessageSizeBytes) {
            _rejected.Add(1, _channelTag);
            throw new ArgumentException("The serialized Redis broadcast exceeds MaxMessageSizeBytes.", nameof(message));
        }
        await connection.GetSubscriber().PublishAsync(_channel, json);
    }

    public async ValueTask SubscribeAsync(
        Func<BroadcastMessage, CancellationToken, ValueTask> listener,
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
            var queue = Channel.CreateBounded<RedisValue>(new BoundedChannelOptions(redisOptions.QueueCapacity) {
                SingleReader = false,
                SingleWriter = false
            });
            var token = lifetime.Token;
            Action<RedisChannel, RedisValue> subscription = (_, message) => {
                if (token.IsCancellationRequested) {
                    return;
                }
                if (message.Length() > redisOptions.MaxMessageSizeBytes) {
                    _dropped.Add(1, _channelTag, new("reason", "oversize"));
                } else if (!queue.Writer.TryWrite(message)) {
                    // TryWrite never waits: drop the newest message instead of growing a Redis-owned queue.
                    _dropped.Add(1, _channelTag, new("reason", "capacity"));
                }
                _received.Add(1, _channelTag);
            };
            try {
                await connection.GetSubscriber().SubscribeAsync(_channel, subscription);
                _subscriptionDeliveries = Task.WhenAll(Enumerable.Range(0, ConcurrentDeliveries)
                    .Select(_ => Task.Run(() => ReceiveAsync(queue.Reader, listener, token))));
                _subscriptionCancellation = lifetime;
                _subscription = subscription;
                _subscriptionQueue = queue;
            } catch {
                await lifetime.CancelAsync();
                queue.Writer.TryComplete();
                try {
                    await connection.GetSubscriber().UnsubscribeAsync(_channel, subscription);
                } finally {
                    lifetime.Dispose();
                }
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
            await connection.GetSubscriber().UnsubscribeAsync(_channel, _subscription);
            _subscriptionQueue!.Writer.TryComplete();
            _ = DisposeWhenCompletedAsync(_subscriptionDeliveries!, _subscriptionCancellation);
            _subscription = null;
            _subscriptionQueue = null;
            _subscriptionCancellation = null;
            _subscriptionDeliveries = null;
        } finally {
            _subscriptionLock.Release();
        }
    }

    private async Task ReceiveAsync(ChannelReader<RedisValue> subscription, Func<BroadcastMessage, CancellationToken, ValueTask> listener, CancellationToken token) {
        try {
            while (true) {
                // Drain the bounded queue after cancellation, without invoking application listeners again.
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

    private async Task HandleMessageAsync(RedisValue message, Func<BroadcastMessage, CancellationToken, ValueTask> listener, CancellationToken cancellationToken) {
        try {
            cancellationToken.ThrowIfCancellationRequested();
            var broadcast = JsonSerializer.Deserialize<BroadcastMessage>((byte[])message!, _jsonOptions);
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
