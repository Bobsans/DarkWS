using System.Collections.Concurrent;
using System.Text.Json;
using DarkBoy.DarkWS.Abstractions;
using DarkBoy.DarkWS.Redis;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace DarkBoy.DarkWS.Redis.Test;

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

    private ServiceProvider CreateProvider(string channel) {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_connection);
        services.AddDarkWs();
        services.AddDarkWsRedis(channel);
        return services.BuildServiceProvider();
    }
}
