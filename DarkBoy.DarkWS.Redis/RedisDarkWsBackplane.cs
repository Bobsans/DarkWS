using System.Text.Json;
using DarkBoy.DarkWS.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace DarkBoy.DarkWS.Redis;

internal sealed class RedisDarkWsBackplane(
    IConnectionMultiplexer connection,
    RedisDarkWsOptions redisOptions,
    IOptions<DarkWsOptions> options,
    ILogger<RedisDarkWsBackplane> logger
) : IDarkWsBackplane {
    private readonly RedisChannel _channel = RedisChannel.Literal(redisOptions.Channel);
    private readonly JsonSerializerOptions _jsonOptions = options.Value.JsonOptions;
    private ChannelMessageQueue? _subscription;
    private Func<DarkWsBroadcast, CancellationToken, ValueTask> _listener =
        static (_, _) => ValueTask.CompletedTask;

    public async ValueTask PublishAsync(
        DarkWsBroadcast message,
        CancellationToken cancellationToken = default
    ) {
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
        _listener = listener;
        _subscription = await connection.GetSubscriber().SubscribeAsync(_channel);
        _subscription.OnMessage(HandleMessageAsync);
    }

    public async ValueTask UnsubscribeAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        if (_subscription is not null) {
            await _subscription.UnsubscribeAsync();
            _subscription = null;
        }
        _listener = static (_, _) => ValueTask.CompletedTask;
    }

    private async Task HandleMessageAsync(ChannelMessage message) {
        try {
            var broadcast = JsonSerializer.Deserialize<DarkWsBroadcast>(message.Message.ToString(), _jsonOptions);
            if (broadcast is not null) {
                await _listener(broadcast, CancellationToken.None);
            }
        } catch (Exception error) {
            logger.LogWarning(error, "Invalid DarkWS Redis message on {Channel}", _channel);
        }
    }
}
