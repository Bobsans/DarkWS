using System.Text.Json;
using DarkWS.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DarkWS.Redis;

internal sealed class RedisDarkWsBackplane(
    IConnectionMultiplexer connection,
    RedisDarkWsOptions redisOptions,
    ILogger<RedisDarkWsBackplane> logger
) : IDarkWsBackplane {
    private readonly RedisChannel _channel = RedisChannel.Literal(redisOptions.Channel);
    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();
    private readonly SemaphoreSlim _subscriptionLock = new(1, 1);
    private ChannelMessageQueue? _subscription;
    private CancellationTokenSource? _subscriptionCancellation;

    public async ValueTask PublishAsync(
        DarkWsBroadcast message,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(message, _jsonOptions);
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
            if (_subscription is not null) throw new InvalidOperationException("The Redis backplane already has a subscriber; unsubscribe before subscribing again");
            var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try {
                var subscription = await connection.GetSubscriber().SubscribeAsync(_channel);
                var token = lifetime.Token;
                subscription.OnMessage(message => HandleMessageAsync(message, listener, token));
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
            if (_subscription is null) return;
            await _subscriptionCancellation!.CancelAsync();
            await _subscription.UnsubscribeAsync();
            _subscription = null;
            _subscriptionCancellation.Dispose();
            _subscriptionCancellation = null;
        } finally {
            _subscriptionLock.Release();
        }
    }

    private async Task HandleMessageAsync(ChannelMessage message, Func<DarkWsBroadcast, CancellationToken, ValueTask> listener, CancellationToken cancellationToken) {
        try {
            cancellationToken.ThrowIfCancellationRequested();
            var broadcast = JsonSerializer.Deserialize<DarkWsBroadcast>(message.Message.ToString(), _jsonOptions);
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
