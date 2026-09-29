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
        var groups = BroadcastTarget.Groups(["a", "missing", "b", "a"]);
        var target = excludeSession ? groups.ExceptSession(session.Id) : groups.ExceptConnection(sender.Id);
        var broadcaster = host.Services.GetRequiredService<IBroadcaster>();
        await broadcaster.PublishAsync(target, "updated", new { Value = 7 });
        Assert.That(host.Broadcasts, Has.Count.EqualTo(1));
        Assert.That(host.Broadcasts.Single().GroupNames, Is.EqualTo(new[] { "a", "missing", "b" }));
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
        await handler.SingleData(BroadcastTarget.Group("a").ExceptSession(sender.Id));
        Assert.That(sender.SentMessages, Has.Count.EqualTo(1));
        Assert.That(collision.SentMessages, Has.Count.EqualTo(1));
        await handler.Many();
        await handler.ManyData(BroadcastTarget.Groups(["a"]).ExceptConnection(sender.Id).ExceptSession(collision.Session!.Id));
        Assert.That(sender.SentMessages, Has.Count.EqualTo(2));
        Assert.That(collision.SentMessages, Has.Count.EqualTo(2));
        Assert.That(other.SentMessages, Has.Count.EqualTo(4));
        using var noData = JsonDocument.Parse(other.SentMessages[0]);
        using var nullData = JsonDocument.Parse(other.SentMessages[1]);
        Assert.That(noData.RootElement.TryGetProperty("data", out _), Is.False);
        Assert.That(nullData.RootElement.GetProperty("data").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(host.Broadcasts.All(message => message.TargetType ==BroadcastTargetType.Groups), Is.True);
    }

    [Test]
    public async Task EmptyUnknownAndInvalidGroupsDoNotPublishOrSendUnexpectedMessages() {
        await using var host = new DarkWsTestHost(_ => { });
        var connection = host.CreateConnection(new HandlerTestingTests.TestSession("session", ["a"]));
        var broadcaster = host.Services.GetRequiredService<IBroadcaster>();
        await broadcaster.PublishAsync(BroadcastTarget.Groups([]), "empty");
        Assert.That(host.Broadcasts, Is.Empty);
        await broadcaster.PublishAsync(BroadcastTarget.Groups(["unknown"]), "unknown");
        await broadcaster.PublishAsync(BroadcastTarget.Groups(["a"]).ExceptSession("missing"), "unmatched-exclusion");
        Assert.That(connection.SentMessages, Has.Count.EqualTo(1));
        var groups = new List<string> { "a" };
        var enumerations = 0;
        IEnumerable<string> Once() {
            enumerations++;
            foreach (var group in groups) {
                yield return group;
            }
        }
        var snapshot = BroadcastTarget.Groups(Once());
        groups[0] = "changed";
        await broadcaster.PublishAsync(snapshot, "snapshot");
        Assert.That(enumerations, Is.EqualTo(1));
        Assert.That(host.Broadcasts.Last().GroupNames, Is.EqualTo(new[] { "a" }));
        Assert.Throws<ArgumentNullException>(() => BroadcastTarget.Groups(null!));
        Assert.Throws<ArgumentNullException>(() => broadcaster.PublishAsync(null!, "invalid"));
        Assert.Throws<ArgumentException>(() => broadcaster.PublishAsync(BroadcastTarget.Groups(["a"]), " "));
        Assert.Throws<ArgumentException>(() => BroadcastTarget.Groups(["a", " "]));
        Assert.Throws<ArgumentException>(() => BroadcastTarget.Groups([null!]));
        Assert.Throws<ArgumentException>(() => BroadcastTarget.Group(" "));
        Assert.Throws<ArgumentException>(() => BroadcastTarget.Groups(["a"]).ExceptConnection(""));
        Assert.Throws<ArgumentException>(() => BroadcastTarget.Groups(["a"]).ExceptSession(" "));
        Assert.Throws<InvalidOperationException>(() => BroadcastTarget.Session("s").ExceptConnection("c"));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => broadcaster.PublishAsync(BroadcastTarget.Groups(["a"]), "cancelled", cancelled.Token));
        Assert.That(host.Broadcasts, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task SingleTargetsWithoutExclusionsKeepLegacyEnvelopes() {
        await using var host = new DarkWsTestHost(_ => { });
        var broadcaster = host.Services.GetRequiredService<IBroadcaster>();
        await broadcaster.PublishAsync(BroadcastTarget.All, "all");
        await broadcaster.PublishAsync(BroadcastTarget.Connection("c"), "connection", 1);
        await broadcaster.PublishAsync(BroadcastTarget.Session("s"), "session");
        await broadcaster.PublishAsync(BroadcastTarget.Group("g"), "group");
        await broadcaster.PublishAsync(BroadcastTarget.Group("g").ExceptConnection("c"), "group-except");
        Assert.That(host.Broadcasts.Select(message => (message.TargetType, message.TargetId)), Is.EqualTo(new (BroadcastTargetType, string?)[] {
            (BroadcastTargetType.All, null), (BroadcastTargetType.Connection, "c"), (BroadcastTargetType.Session, "s"), (BroadcastTargetType.Group, "g"), (BroadcastTargetType.Groups, null)
        }));
        Assert.That(host.Broadcasts.Last().GroupNames, Is.EqualTo(new[] { "g" }));
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
        Assert.Throws<ArgumentException>(() => JsonSerializer.Deserialize<BroadcastMessage>(json));
    }

    [Test]
    public async Task LegacyEnvelopeAndCustomBroadcasterRemainUsable() {
        var legacy = new BroadcastMessage(BroadcastTargetType.Group, "a", "changed", null);
        var json = JsonSerializer.Serialize(legacy);
        Assert.That(json, Is.EqualTo("{\"target\":3,\"targetId\":\"a\",\"action\":\"changed\"}"));
        Assert.That(JsonSerializer.Deserialize<BroadcastMessage>(json), Is.EqualTo(legacy));
        var custom = new CustomBroadcaster();
        IBroadcaster broadcaster = custom;
        await broadcaster.PublishAsync(BroadcastTarget.Group("a"), "b");
        await broadcaster.PublishAsync(BroadcastTarget.All, "c", 1);
        Assert.That(custom.Calls, Is.EqualTo(new[] { "b", "c" }));
    }

    private sealed class GroupHandler : HandlerBase {
        public Task Single() => PublishAsync(BroadcastTarget.Group("a").ExceptConnection(Connection.Id), "single");
        public Task SingleData(BroadcastTarget target) => PublishAsync<string?>(target, "single-data", null);
        public Task Many() => PublishAsync(BroadcastTarget.Groups(["a", "a"]), "many");
        public Task ManyData(BroadcastTarget target) => PublishAsync(target, "many-data", 1);
    }

    // Implements only the two required members; the token-less overloads come from the interface.
    private sealed class CustomBroadcaster : IBroadcaster {
        public List<string> Calls { get; } = [];

        public Task PublishAsync(BroadcastTarget target, string action, CancellationToken cancellationToken) {
            Calls.Add(action);
            return Task.CompletedTask;
        }

        public Task PublishAsync<T>(BroadcastTarget target, string action, T? data, CancellationToken cancellationToken) {
            Calls.Add(action);
            return Task.CompletedTask;
        }
    }
}
