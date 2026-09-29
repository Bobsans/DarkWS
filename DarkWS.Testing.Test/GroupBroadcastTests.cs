using System.Text.Json;
using DarkWS.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DarkWS.Testing.Test;

public sealed class GroupBroadcastTests {
    [TestCase(false)]
    [TestCase(true)]
    public async Task GroupUnionPublishesOnceAndExcludesConnectionOrWholeSession(bool excludeSession) {
        await using var host = new DarkWsTestHost(_ => { });
        var session = new HandlerTestingTests.TestSession("sender", ["a", "b"]);
        var sender = host.CreateConnection(session);
        var otherTab = host.CreateConnection(session);
        var otherUser = host.CreateConnection(new HandlerTestingTests.TestSession("other", ["a", "b", "b"]));
        var unrelated = host.CreateConnection(new HandlerTestingTests.TestSession("unrelated", ["c"]));
        var except = excludeSession ? new DarkWsBroadcastExclusion { SessionId = session.Id }
            : new DarkWsBroadcastExclusion { ConnectionId = sender.Id };
        var broadcaster = host.Services.GetRequiredService<IBroadcaster>();
        await broadcaster.BroadcastToGroupsAsync(["a", "missing", "b", "a"], "updated", new { Value = 7 }, except);
        Assert.That(host.Broadcasts, Has.Count.EqualTo(1));
        Assert.That(host.Broadcasts.Single().Groups, Is.EqualTo(new[] { "a", "missing", "b" }));
        Assert.That(sender.SentMessages, Is.Empty);
        Assert.That(otherTab.SentMessages, Has.Count.EqualTo(excludeSession ? 0 : 1));
        Assert.That(otherUser.SentMessages, Has.Count.EqualTo(1));
        Assert.That(unrelated.SentMessages, Is.Empty);
        using var message = JsonDocument.Parse(otherUser.SentMessages.Single());
        Assert.That(message.RootElement.GetProperty("data").GetProperty("value").GetInt32(), Is.EqualTo(7));
        Assert.That(message.RootElement.EnumerateObject().Count(), Is.EqualTo(3));
    }

    [Test]
    public async Task HandlerHelpersPreserveNoDataNullDataAndExplicitExclusionKinds() {
        await using var host = new DarkWsTestHost(_ => { });
        var sender = host.CreateConnection(new HandlerTestingTests.TestSession("session", ["a"]));
        var collision = host.CreateConnection(new HandlerTestingTests.TestSession(sender.Id, ["a"]));
        var other = host.CreateConnection(new HandlerTestingTests.TestSession("other", ["a"]));
        await using var scope = host.CreateScope(sender);
        var handler = new GroupHandler();
        scope.Initialize(handler);
        await handler.Single();
        Assert.That(sender.SentMessages, Is.Empty);
        Assert.That(collision.SentMessages, Has.Count.EqualTo(1));
        await handler.SingleData(new DarkWsBroadcastExclusion { SessionId = sender.Id });
        Assert.That(sender.SentMessages, Has.Count.EqualTo(1));
        Assert.That(collision.SentMessages, Has.Count.EqualTo(1));
        await handler.Many();
        await handler.ManyData(new DarkWsBroadcastExclusion { ConnectionId = sender.Id, SessionId = collision.Session!.Id });
        Assert.That(sender.SentMessages, Has.Count.EqualTo(2));
        Assert.That(collision.SentMessages, Has.Count.EqualTo(2));
        Assert.That(other.SentMessages, Has.Count.EqualTo(4));
        using var noData = JsonDocument.Parse(other.SentMessages[0]);
        using var nullData = JsonDocument.Parse(other.SentMessages[1]);
        Assert.That(noData.RootElement.TryGetProperty("data", out _), Is.False);
        Assert.That(nullData.RootElement.GetProperty("data").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(host.Broadcasts.All(message => message.Target == DarkWsTarget.Groups), Is.True);
    }

    [Test]
    public async Task EmptyUnknownAndInvalidGroupsDoNotPublishOrSendUnexpectedMessages() {
        await using var host = new DarkWsTestHost(_ => { });
        var connection = host.CreateConnection(new HandlerTestingTests.TestSession("session", ["a"]));
        var broadcaster = host.Services.GetRequiredService<IBroadcaster>();
        await broadcaster.BroadcastToGroupsAsync([], "empty");
        Assert.That(host.Broadcasts, Is.Empty);
        await broadcaster.BroadcastToGroupsAsync(["unknown"], "unknown");
        await broadcaster.BroadcastToGroupsAsync(["a"], "unmatched-exclusion", except: new DarkWsBroadcastExclusion { SessionId = "missing" });
        Assert.That(connection.SentMessages, Has.Count.EqualTo(1));
        var groups = new List<string> { "a" };
        var enumerations = 0;
        IEnumerable<string> Once() {
            enumerations++;
            foreach (var group in groups) yield return group;
        }
        await broadcaster.BroadcastToGroupsAsync(Once(), "snapshot");
        groups[0] = "changed";
        Assert.That(enumerations, Is.EqualTo(1));
        Assert.That(host.Broadcasts.Last().Groups, Is.EqualTo(new[] { "a" }));
        Assert.Throws<ArgumentNullException>(() => broadcaster.BroadcastToGroupsAsync(null!, "invalid"));
        Assert.Throws<ArgumentException>(() => broadcaster.BroadcastToGroupsAsync(["a"], " "));
        Assert.Throws<ArgumentException>(() => broadcaster.BroadcastToGroupsAsync(["a", " "], "invalid"));
        Assert.Throws<ArgumentException>(() => broadcaster.BroadcastToGroupsAsync([null!], "invalid"));
        Assert.Throws<ArgumentException>(() => broadcaster.BroadcastToGroupsAsync(["a"], "invalid", except: new DarkWsBroadcastExclusion { ConnectionId = "" }));
        Assert.Throws<ArgumentException>(() => broadcaster.BroadcastToGroupsAsync(["a"], "invalid", except: new DarkWsBroadcastExclusion { SessionId = " " }));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => broadcaster.BroadcastToGroupsAsync(["a"], "cancelled", cancellationToken: cancelled.Token));
        Assert.That(host.Broadcasts, Has.Count.EqualTo(3));
    }

    [TestCase("{\"target\":4,\"action\":\"a\"}")]
    [TestCase("{\"target\":4,\"action\":\"a\",\"groups\":[null]}")]
    [TestCase("{\"target\":4,\"action\":\"a\",\"groups\":[\" \" ]}")]
    [TestCase("{\"target\":4,\"action\":\" \",\"groups\":[\"a\"]}")]
    [TestCase("{\"target\":4,\"targetId\":\"a\",\"action\":\"a\",\"groups\":[\"a\"]}")]
    [TestCase("{\"target\":3,\"targetId\":\"a\",\"action\":\"a\",\"groups\":[\"a\"]}")]
    [TestCase("{\"target\":3,\"targetId\":\"a\",\"action\":\"a\",\"except\":{\"connectionId\":\"x\"}}")]
    [TestCase("{\"target\":4,\"action\":\"a\",\"groups\":[\"a\"],\"except\":{\"sessionId\":\" \"}}")]
    public void MalformedGroupSelectionIsRejectedDuringDeserialization(string json) {
        Assert.Throws<ArgumentException>(() => JsonSerializer.Deserialize<DarkWsBroadcast>(json));
    }

    [Test]
    public void LegacyEnvelopeAndCustomBroadcasterRemainUsable() {
        var legacy = new DarkWsBroadcast(DarkWsTarget.Group, "a", "changed", null);
        var json = JsonSerializer.Serialize(legacy);
        Assert.That(json, Is.EqualTo("{\"target\":3,\"targetId\":\"a\",\"action\":\"changed\"}"));
        Assert.That(JsonSerializer.Deserialize<DarkWsBroadcast>(json), Is.EqualTo(legacy));
        IBroadcaster custom = new LegacyBroadcaster();
        Assert.DoesNotThrow(() => custom.BroadcastToGroupAsync("a", "b"));
        Assert.Throws<NotSupportedException>(() => custom.BroadcastToGroupsAsync(["a"], "b"));
        Assert.Throws<NotSupportedException>(() => custom.BroadcastToGroupsAsync(["a"], "b", 1));
    }

    private sealed class GroupHandler : HandlerBase {
        public Task Single() => BroadcastToGroupAsync("a", "single", new DarkWsBroadcastExclusion { ConnectionId = Connection.Id });
        public Task SingleData(DarkWsBroadcastExclusion except) => BroadcastToGroupAsync<string?>("a", "single-data", null, except);
        public Task Many() => BroadcastToGroupsAsync(["a", "a"], "many");
        public Task ManyData(DarkWsBroadcastExclusion except) => BroadcastToGroupsAsync(["a"], "many-data", 1, except);
    }

    private sealed class LegacyBroadcaster : IBroadcaster {
        public Task BroadcastAsync(string action, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BroadcastAsync<T>(string action, T? data, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BroadcastToConnectionAsync(string connectionId, string action, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BroadcastToConnectionAsync<T>(string connectionId, string action, T? data, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BroadcastToSessionAsync(string sessionId, string action, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BroadcastToSessionAsync<T>(string sessionId, string action, T? data, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BroadcastToGroupAsync(string group, string action, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BroadcastToGroupAsync<T>(string group, string action, T? data, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
