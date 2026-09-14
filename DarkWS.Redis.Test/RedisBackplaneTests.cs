using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using DarkWS.Abstractions;
using DarkWS.Redis;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace DarkWS.Redis.Test;

public sealed class RedisBackplaneTests {
    private RedisContainer _redis = null!;
    private IConnectionMultiplexer _connection = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync() {
        _redis = new RedisBuilder("redis:7.2-alpine").Build();
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync() {
        await _connection.DisposeAsync();
        await _redis.DisposeAsync();
    }

    [Test]
    public async Task BroadcastCrossesInstancesExactlyOnceAsync() {
        var channel = $"darkws-test:{Guid.NewGuid():N}";
        await using var firstProvider = CreateProvider(channel);
        await using var secondProvider = CreateProvider(channel);
        var first = firstProvider.GetRequiredService<IDarkWsBackplane>();
        var second = secondProvider.GetRequiredService<IDarkWsBackplane>();
        var received = new ConcurrentBag<DarkWsBroadcast>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        ValueTask Listen(DarkWsBroadcast message, CancellationToken _) {
            received.Add(message);
            if (received.Count == 2) {
                completed.TrySetResult();
            }
            return ValueTask.CompletedTask;
        }

        await first.SubscribeAsync(Listen);
        await second.SubscribeAsync(Listen);
        var message = new DarkWsBroadcast(
            DarkWsTarget.Group,
            "account:42",
            "changed",
            JsonSerializer.SerializeToElement(new { Value = 1 })
        );

        await first.PublishAsync(message);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(received, Has.Count.EqualTo(2));
        Assert.That(received.All(it => it.Target == DarkWsTarget.Group), Is.True);
        Assert.That(received.All(it => it.TargetId == "account:42"), Is.True);
        Assert.That(received.All(it => it.Action == "changed"), Is.True);
    }

    [Test]
    public void EmptyChannelIsRejected() {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddDarkWsRedis(" "));
    }

    [Test]
    public void NullListenerIsRejected() {
        var channel = $"darkws-test:{Guid.NewGuid():N}";
        using var provider = CreateProvider(channel);
        var backplane = provider.GetRequiredService<IDarkWsBackplane>();

        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await backplane.SubscribeAsync(null!));
    }

    [Test]
    public void UnsubscribeBeforeSubscribeIsANoop() {
        var channel = $"darkws-test:{Guid.NewGuid():N}";
        using var provider = CreateProvider(channel);
        var backplane = provider.GetRequiredService<IDarkWsBackplane>();

        Assert.DoesNotThrowAsync(async () => {
            await backplane.UnsubscribeAsync();
            await backplane.UnsubscribeAsync();
        });
    }

    [Test]
    public async Task UnsubscribeStopsDeliveryAsync() {
        var channel = $"darkws-test:{Guid.NewGuid():N}";
        await using var provider = CreateProvider(channel);
        var backplane = provider.GetRequiredService<IDarkWsBackplane>();
        var count = 0;
        await backplane.SubscribeAsync((_, _) => {
            Interlocked.Increment(ref count);
            return ValueTask.CompletedTask;
        });
        await backplane.UnsubscribeAsync();

        await backplane.PublishAsync(new DarkWsBroadcast(DarkWsTarget.All, null, "ignored", null));
        await Task.Delay(100);

        Assert.That(count, Is.Zero);
    }

    [Test]
    public async Task MalformedRedisMessageDoesNotBreakFollowingDeliveryAsync() {
        var channel = $"darkws-test:{Guid.NewGuid():N}";
        await using var provider = CreateProvider(channel);
        var backplane = provider.GetRequiredService<IDarkWsBackplane>();
        var received = new TaskCompletionSource<DarkWsBroadcast>(TaskCreationOptions.RunContinuationsAsynchronously);
        await backplane.SubscribeAsync((message, _) => {
            received.TrySetResult(message);
            return ValueTask.CompletedTask;
        });
        await _connection.GetSubscriber().PublishAsync(RedisChannel.Literal(channel), "{");

        await backplane.PublishAsync(new DarkWsBroadcast(DarkWsTarget.All, null, "valid", null));

        Assert.That((await received.Task.WaitAsync(TimeSpan.FromSeconds(10))).Action, Is.EqualTo("valid"));
    }

    [Test]
    public void CancelledOperationsStopBeforeRedisIo() {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var channel = $"darkws-test:{Guid.NewGuid():N}";
        using var provider = CreateProvider(channel);
        var backplane = provider.GetRequiredService<IDarkWsBackplane>();
        var message = new DarkWsBroadcast(DarkWsTarget.All, null, "cancelled", null);

        Assert.Multiple(() => {
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await backplane.PublishAsync(message, cancellation.Token));
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await backplane.SubscribeAsync((_, _) => ValueTask.CompletedTask, cancellation.Token));
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await backplane.UnsubscribeAsync(cancellation.Token));
        });
    }

    [Test]
    public async Task BackplaneEnvelopeIgnoresApplicationJsonOptions() {
        var channel = $"darkws-test:{Guid.NewGuid():N}";
        await using var firstProvider = CreateProvider(channel, options => {
            options.JsonOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.JsonOptions.Converters.Add(new JsonStringEnumConverter());
        });
        await using var secondProvider = CreateProvider(channel, options => options.JsonOptions.PropertyNamingPolicy = null);
        var first = firstProvider.GetRequiredService<IDarkWsBackplane>();
        var second = secondProvider.GetRequiredService<IDarkWsBackplane>();
        var received = new TaskCompletionSource<DarkWsBroadcast>(TaskCreationOptions.RunContinuationsAsynchronously);
        var raw = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var wire = await _connection.GetSubscriber().SubscribeAsync(RedisChannel.Literal(channel));
        wire.OnMessage(message => raw.TrySetResult(message.Message.ToString()));
        await second.SubscribeAsync((message, _) => { received.TrySetResult(message); return ValueTask.CompletedTask; });
        try {
            var data = JsonSerializer.SerializeToElement(new { DisplayName = "Ada" }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            await first.PublishAsync(new DarkWsBroadcast(DarkWsTarget.Group, "group", "changed", data));
            var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var envelope = JsonDocument.Parse(await raw.Task.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.That(envelope.RootElement.GetProperty("target").GetInt32(), Is.EqualTo(3));
            Assert.That(envelope.RootElement.GetProperty("targetId").GetString(), Is.EqualTo("group"));
            Assert.That(result.Target, Is.EqualTo(DarkWsTarget.Group));
            Assert.That(result.Data!.Value.GetProperty("display_name").GetString(), Is.EqualTo("Ada"));
        } finally {
            await second.UnsubscribeAsync();
            await wire.UnsubscribeAsync();
        }
    }

    [Test]
    public async Task RepeatedSubscriptionIsRejectedAndResubscribeUsesOnlyNewListener() {
        await using var provider = CreateProvider($"darkws-test:{Guid.NewGuid():N}");
        var backplane = provider.GetRequiredService<IDarkWsBackplane>();
        var oldCalls = 0;
        await backplane.SubscribeAsync((_, _) => { Interlocked.Increment(ref oldCalls); return ValueTask.CompletedTask; });
        Assert.ThrowsAsync<InvalidOperationException>(async () => await backplane.SubscribeAsync((_, _) => ValueTask.CompletedTask));
        await backplane.UnsubscribeAsync();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        await backplane.SubscribeAsync((_, _) => { Interlocked.Increment(ref calls); received.TrySetResult(); return ValueTask.CompletedTask; });
        try {
            await backplane.PublishAsync(new DarkWsBroadcast(DarkWsTarget.All, null, "new", null));
            await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(oldCalls, Is.Zero);
        } finally {
            await backplane.UnsubscribeAsync();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SubscriptionLifetimeCancelsActiveDelivery(bool cancelCaller) {
        await using var provider = CreateProvider($"darkws-test:{Guid.NewGuid():N}");
        var backplane = provider.GetRequiredService<IDarkWsBackplane>();
        using var lifetime = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await backplane.SubscribeAsync(async (_, token) => {
            started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { if (token.IsCancellationRequested) stopped.TrySetResult(); }
        }, lifetime.Token);
        await backplane.PublishAsync(new DarkWsBroadcast(DarkWsTarget.All, null, "blocked", null));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (cancelCaller) lifetime.Cancel();
        else await backplane.UnsubscribeAsync();
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (cancelCaller) await backplane.UnsubscribeAsync();
    }

    private ServiceProvider CreateProvider(string channel, Action<DarkWsOptions>? configure = null) {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_connection);
        services.AddDarkWs(configure);
        services.AddDarkWsRedis(channel);
        return services.BuildServiceProvider();
    }
}
