using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using DarkWS.Abstractions;
using DarkWS.Testing;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    public void RepeatedRegistrationIsRejected() {
        var services = new ServiceCollection();
        services.AddDarkWsRedis("first");

        var error = Assert.Throws<InvalidOperationException>(() => services.AddDarkWsRedis("second"))!;

        Assert.That(error.Message, Does.Contain("AddDarkWsRedis"));
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
        await using var controlProvider = CreateProvider(channel);
        var backplane = provider.GetRequiredService<IDarkWsBackplane>();
        var control = controlProvider.GetRequiredService<IDarkWsBackplane>();
        var count = 0;
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await backplane.SubscribeAsync((_, _) => {
            Interlocked.Increment(ref count);
            return ValueTask.CompletedTask;
        });
        await control.SubscribeAsync((_, _) => {
            delivered.TrySetResult();
            return ValueTask.CompletedTask;
        });
        await backplane.UnsubscribeAsync();

        try {
            await backplane.PublishAsync(new DarkWsBroadcast(DarkWsTarget.All, null, "ignored", null));
            // The control subscriber on the same channel proves the message went through Redis.
            await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        } finally {
            await control.UnsubscribeAsync();
        }

        Assert.That(count, Is.Zero);
    }

    [TestCase("{")]
    [TestCase("{\"target\":4,\"action\":\"bad\"}")]
    [TestCase("{\"target\":4,\"action\":\"bad\",\"groups\":[null]}")]
    [TestCase("{\"target\":3,\"targetId\":\"g\",\"action\":\"bad\",\"except\":{\"sessionId\":\"s\"}}")]
    public async Task MalformedRedisMessageDoesNotBreakFollowingDeliveryAsync(string invalid) {
        var channel = $"darkws-test:{Guid.NewGuid():N}";
        await using var provider = CreateProvider(channel);
        var backplane = provider.GetRequiredService<IDarkWsBackplane>();
        var received = new TaskCompletionSource<DarkWsBroadcast>(TaskCreationOptions.RunContinuationsAsynchronously);
        await backplane.SubscribeAsync((message, _) => {
            received.TrySetResult(message);
            return ValueTask.CompletedTask;
        });
        await _connection.GetSubscriber().PublishAsync(RedisChannel.Literal(channel), invalid);

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
        await second.SubscribeAsync((message, _) => {
            received.TrySetResult(message);
            return ValueTask.CompletedTask;
        });
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
        await backplane.SubscribeAsync((_, _) => {
            Interlocked.Increment(ref oldCalls);
            return ValueTask.CompletedTask;
        });
        Assert.ThrowsAsync<InvalidOperationException>(async () => await backplane.SubscribeAsync((_, _) => ValueTask.CompletedTask));
        await backplane.UnsubscribeAsync();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        await backplane.SubscribeAsync((_, _) => {
            Interlocked.Increment(ref calls);
            received.TrySetResult();
            return ValueTask.CompletedTask;
        });
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
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); } finally {
                if (token.IsCancellationRequested) {
                    stopped.TrySetResult();
                }
            }
        }, lifetime.Token);
        await backplane.PublishAsync(new DarkWsBroadcast(DarkWsTarget.All, null, "blocked", null));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (cancelCaller) {
            lifetime.Cancel();
        } else {
            await backplane.UnsubscribeAsync();
        }

        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (cancelCaller) {
            await backplane.UnsubscribeAsync();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task GroupUnionAndExclusionsAreDeliveredOnceAcrossInstances(bool excludeSession) {
        var channel = $"darkws-test:{Guid.NewGuid():N}";
        await using var first = CreateProvider(channel);
        await using var second = CreateProvider(channel);
        var firstService = first.GetRequiredService<IHostedService>();
        var secondService = second.GetRequiredService<IHostedService>();
        await firstService.StartAsync(default);
        await secondService.StartAsync(default);
        using var sender = new DarkWsTestConnection(new GroupSession("user", ["a", "b"]));
        using var otherTab = new DarkWsTestConnection(new GroupSession("user", ["a", "b"]));
        using var otherUser = new DarkWsTestConnection(new GroupSession("other", ["a", "b"]));
        using var unrelated = new DarkWsTestConnection(new GroupSession("unrelated", ["c"]));
        first.GetRequiredService<ConnectionStorage>().Add(sender);
        var secondStorage = second.GetRequiredService<ConnectionStorage>();
        secondStorage.Add(otherTab);
        secondStorage.Add(otherUser);
        secondStorage.Add(unrelated);
        var publications = new ConcurrentQueue<string>();
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wire = await _connection.GetSubscriber().SubscribeAsync(RedisChannel.Literal(channel));
        wire.OnMessage(message => {
            using var json = JsonDocument.Parse(message.Message.ToString());
            if (json.RootElement.GetProperty("action").GetString() == "updated") {
                publications.Enqueue(message.Message.ToString());
            } else {
                barrier.TrySetResult();
            }
        });
        try {
            var except = excludeSession ? new DarkWsBroadcastExclusion { SessionId = "user" }
                : new DarkWsBroadcastExclusion { ConnectionId = sender.Id };
            var broadcaster = first.GetRequiredService<IBroadcaster>();
            await broadcaster.BroadcastToGroupsAsync<string?>(["a", "b", "a"], "updated", null, except);
            // Each subscriber processes the barrier after the tested broadcast; no sleep-based absence assertion.
            await broadcaster.BroadcastAsync("barrier");
            await barrier.Task.WaitAsync(TimeSpan.FromSeconds(10));
            DarkWsTestConnection[] connections = [sender, otherTab, otherUser, unrelated];
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (connections.Any(connection => !connection.SentMessages.Any(bytes => HasAction(bytes, "barrier")))) {
                await Task.Delay(10, deadline.Token);
            }
            Assert.That(publications, Has.Count.EqualTo(1));
            using var envelope = JsonDocument.Parse(publications.Single());
            Assert.That(envelope.RootElement.GetProperty("target").GetInt32(), Is.EqualTo(4));
            Assert.That(envelope.RootElement.GetProperty("groups").GetArrayLength(), Is.EqualTo(2));
            Assert.That(envelope.RootElement.GetProperty("except").GetProperty(excludeSession ? "sessionId" : "connectionId").GetString(),
                Is.EqualTo(excludeSession ? "user" : sender.Id));
            Assert.That(sender.SentMessages.Count(bytes => HasAction(bytes, "updated")), Is.Zero);
            Assert.That(otherTab.SentMessages.Count(bytes => HasAction(bytes, "updated")), Is.EqualTo(excludeSession ? 0 : 1));
            Assert.That(otherUser.SentMessages.Count(bytes => HasAction(bytes, "updated")), Is.EqualTo(1));
            Assert.That(unrelated.SentMessages.Count(bytes => HasAction(bytes, "updated")), Is.Zero);
            using var delivered = JsonDocument.Parse(otherUser.SentMessages.Single(bytes => HasAction(bytes, "updated")));
            Assert.That(delivered.RootElement.GetProperty("data").ValueKind, Is.EqualTo(JsonValueKind.Null));
        } finally {
            await wire.UnsubscribeAsync();
            await firstService.StopAsync(default);
            await secondService.StopAsync(default);
        }
    }

    private static bool HasAction(byte[] bytes, string action) {
        using var json = JsonDocument.Parse(bytes);
        return json.RootElement.GetProperty("action").GetString() == action;
    }

    private sealed class GroupSession(string id, string[] groups) : IDarkWsSession {
        public string Id => id;
        public IReadOnlyCollection<string> Groups => groups;
        public ClaimsPrincipal User { get; } = new(new ClaimsIdentity([], "test"));
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
