using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using DarkWS.Abstractions;
using DarkWS.Test.Project;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace DarkWS.Test;

public sealed class BroadcastTests {
    [Test]
    public async Task BroadcastAllReachesEveryLocalConnectionAsync() {
        using var host = CreateHost();
        await host.StartAsync();
        var firstSocket = new TestWebSocket();
        var secondSocket = new TestWebSocket();
        var storage = host.Services.GetRequiredService<ConnectionStorage>();
        storage.Add(CreateConnection(firstSocket, "one"));
        storage.Add(CreateConnection(secondSocket, "two"));

        await host.Services.GetRequiredService<IBroadcaster>().PublishAsync(BroadcastTarget.All, "refreshed");

        Assert.That(ReadAction(firstSocket), Is.EqualTo("refreshed"));
        Assert.That(ReadAction(secondSocket), Is.EqualTo("refreshed"));
        await host.StopAsync();
    }

    [Test]
    public async Task BroadcastToConnectionReachesOnlyMatchingConnectionAsync() {
        using var host = CreateHost();
        await host.StartAsync();
        var matchingSocket = new TestWebSocket();
        var otherSocket = new TestWebSocket();
        var matching = CreateConnection(matchingSocket, "one");
        var storage = host.Services.GetRequiredService<ConnectionStorage>();
        storage.Add(matching);
        storage.Add(CreateConnection(otherSocket, "two"));

        await host.Services.GetRequiredService<IBroadcaster>()
            .PublishAsync(BroadcastTarget.Connection(matching.Id), "private");

        Assert.That(ReadAction(matchingSocket), Is.EqualTo("private"));
        Assert.That(otherSocket.Sent, Is.Empty);
        await host.StopAsync();
    }

    [Test]
    public async Task BroadcastToSessionReachesEverySessionConnectionAsync() {
        using var host = CreateHost();
        await host.StartAsync();
        var firstSocket = new TestWebSocket();
        var secondSocket = new TestWebSocket();
        var otherSocket = new TestWebSocket();
        var storage = host.Services.GetRequiredService<ConnectionStorage>();
        storage.Add(CreateConnection(firstSocket, "shared"));
        storage.Add(CreateConnection(secondSocket, "shared"));
        storage.Add(CreateConnection(otherSocket, "other"));

        await host.Services.GetRequiredService<IBroadcaster>()
            .PublishAsync(BroadcastTarget.Session("shared"), "session", new { Value = 42 });

        Assert.That(ReadAction(firstSocket), Is.EqualTo("session"));
        Assert.That(ReadAction(secondSocket), Is.EqualTo("session"));
        Assert.That(otherSocket.Sent, Is.Empty);
        await host.StopAsync();
    }

    [Test]
    public async Task BroadcastToGroupReachesOnlyGroupMembersAsync() {
        using var host = CreateHost();
        await host.StartAsync();
        var storage = host.Services.GetRequiredService<ConnectionStorage>();
        var matchingSocket = new TestWebSocket();
        var otherSocket = new TestWebSocket();
        storage.Add(CreateConnection(matchingSocket, "one"));
        storage.Add(CreateConnection(otherSocket, "two"));

        await host.Services.GetRequiredService<IBroadcaster>()
            .PublishAsync(BroadcastTarget.Group("session:one"), "updated", new { Value = 1 });

        Assert.That(matchingSocket.Sent, Has.Count.EqualTo(1));
        Assert.That(otherSocket.Sent, Is.Empty);
        using var message = JsonDocument.Parse(matchingSocket.Sent.Single());
        Assert.That(message.RootElement.GetProperty("id").GetString(), Is.EqualTo("@"));
        Assert.That(message.RootElement.GetProperty("action").GetString(), Is.EqualTo("updated"));
        Assert.That(message.RootElement.GetProperty("data").GetProperty("value").GetInt32(), Is.EqualTo(1));
        Assert.That(message.RootElement.EnumerateObject().Count(), Is.EqualTo(3));
        await host.StopAsync();
    }

    [Test]
    public async Task ConcurrentSendsNeverOverlapOnOneSocketAsync() {
        var socket = new TestWebSocket { SendDelay = TimeSpan.FromMilliseconds(20) };
        using var connection = CreateConnection(socket, "one");

        await Task.WhenAll(
            connection.SendAsync(new byte[] { 1 }),
            connection.SendAsync(new byte[] { 2 })
        );

        Assert.That(socket.MaxConcurrentSends, Is.EqualTo(1));
    }

    [Test]
    public async Task LocalConnectionTargetSkipsBackplaneAsync() {
        var backplane = new RecordingBackplane();
        using var host = CreateHost(backplane: backplane);
        await host.StartAsync();
        var socket = new TestWebSocket();
        var connection = CreateConnection(socket, "one");
        host.Services.GetRequiredService<ConnectionStorage>().Add(connection);

        await host.Services.GetRequiredService<IBroadcaster>()
            .PublishAsync(BroadcastTarget.Connection(connection.Id), "private", new { Value = 1 });

        Assert.That(ReadAction(socket), Is.EqualTo("private"));
        Assert.That(backplane.Published, Is.Empty);
        await host.StopAsync();
    }

    [Test]
    public async Task LocalConnectionSendsTheSameBytesAsBackplaneDeliveryAsync() {
        using var host = CreateHost();
        await host.StartAsync();
        var socket = new TestWebSocket();
        var connection = CreateConnection(socket, "one");
        host.Services.GetRequiredService<ConnectionStorage>().Add(connection);
        var broadcaster = host.Services.GetRequiredService<IBroadcaster>();
        BroadcastTarget[] targets = [BroadcastTarget.Connection(connection.Id), BroadcastTarget.Session("one")];

        foreach (var target in targets) {
            await broadcaster.PublishAsync(target, "none");
            await broadcaster.PublishAsync<string?>(target, "null", null);
            await broadcaster.PublishAsync(target, "data", new { Value = 1, Items = new[] { "a" } });
        }

        var sent = socket.Sent.Select(System.Text.Encoding.UTF8.GetString).ToArray();
        string[] local = sent[..3], remote = sent[3..];
        Assert.That(local, Is.EqualTo(remote));
        Assert.That(sent[1], Does.Contain("\"data\":null"));
        Assert.That(sent[0], Does.Not.Contain("data"));
        await host.StopAsync();
    }

    [Test]
    public async Task UnknownConnectionAndOtherTargetsUseBackplaneAsync() {
        var backplane = new RecordingBackplane();
        using var host = CreateHost(backplane: backplane);
        await host.StartAsync();
        var socket = new TestWebSocket();
        var connection = CreateConnection(socket, "one");
        host.Services.GetRequiredService<ConnectionStorage>().Add(connection);
        var broadcaster = host.Services.GetRequiredService<IBroadcaster>();

        await broadcaster.PublishAsync(BroadcastTarget.Connection("remote"), "remote");
        await broadcaster.PublishAsync(BroadcastTarget.Session("one"), "session");
        await broadcaster.PublishAsync(BroadcastTarget.Group("session:one"), "group");
        await broadcaster.PublishAsync(BroadcastTarget.Group("session:one").ExceptConnection("other"), "group-except");
        await broadcaster.PublishAsync(BroadcastTarget.Groups(["session:one"]), "groups");

        Assert.That(backplane.Published.Select(message => (message.TargetType, message.TargetId)), Is.EqualTo(new (BroadcastTargetType, string?)[] {
            (BroadcastTargetType.Connection, "remote"), (BroadcastTargetType.Session, "one"), (BroadcastTargetType.Group, "session:one"),
            (BroadcastTargetType.Groups, null), (BroadcastTargetType.Groups, null)
        }));
        Assert.That(socket.Sent, Has.Count.EqualTo(4));
        await host.StopAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task TimedOutBroadcastAbortsConnectionAsync(bool local) {
        using var host = CreateHost(TimeSpan.FromMilliseconds(10));
        await host.StartAsync();
        var socket = new TestWebSocket { SendDelay = TimeSpan.FromSeconds(1) };
        var connection = CreateConnection(socket, "one");
        host.Services.GetRequiredService<ConnectionStorage>().Add(connection);

        await host.Services.GetRequiredService<IBroadcaster>()
            .PublishAsync(local ? BroadcastTarget.Connection(connection.Id) : BroadcastTarget.All, "updated", new { Value = 1 });

        Assert.That(socket.WasAborted, Is.True);
        await host.StopAsync();
    }

    [Test]
    public async Task TargetedOverloadsPreserveTargetAndDataAsync() {
        using var host = CreateHost();
        await host.StartAsync();
        var socket = new TestWebSocket();
        var connection = CreateConnection(socket, "one");
        host.Services.GetRequiredService<ConnectionStorage>().Add(connection);
        var broadcaster = host.Services.GetRequiredService<IBroadcaster>();

        await broadcaster.PublishAsync(BroadcastTarget.Connection(connection.Id), "connection-data", new { Value = 1 });
        await broadcaster.PublishAsync(BroadcastTarget.Session("one"), "session-empty");
        await broadcaster.PublishAsync(BroadcastTarget.Group("session:one"), "group-empty");

        var actions = socket.Sent.Select(data => {
            using var message = JsonDocument.Parse(data);
            return message.RootElement.GetProperty("action").GetString();
        });
        Assert.That(actions, Is.EqualTo(new[] { "connection-data", "session-empty", "group-empty" }));
        await host.StopAsync();
    }

    [Test]
    public async Task BroadcastSkipsClosedConnectionsAsync() {
        using var host = CreateHost();
        await host.StartAsync();
        var socket = new TestWebSocket();
        host.Services.GetRequiredService<ConnectionStorage>().Add(CreateConnection(socket, "one"));
        socket.SetState(WebSocketState.Closed);

        await host.Services.GetRequiredService<IBroadcaster>().PublishAsync(BroadcastTarget.All, "ignored", new { Value = 1 });

        Assert.That(socket.Sent, Is.Empty);
        await host.StopAsync();
    }

    [Test]
    public void ConnectionStorageAddsQueriesAndRemovesSnapshots() {
        var storage = new ConnectionStorage();
        using var first = CreateConnection(new TestWebSocket(), "one");
        using var second = CreateConnection(new TestWebSocket(), "two");

        Assert.That(storage.Add(first), Is.SameAs(first));
        storage.Add(second);
        Assert.Multiple(() => {
            Assert.That(storage.GetAll(), Has.Count.EqualTo(2));
            Assert.That(storage.GetByConnection(first.Id), Is.EqualTo(new[] { first }));
            Assert.That(storage.GetByConnection("missing"), Is.Empty);
            Assert.That(storage.GetBySession("one"), Is.EqualTo(new[] { first }));
            Assert.That(storage.GetByGroup("session:two"), Is.EqualTo(new[] { second }));
        });
        Assert.That(storage.Remove(first), Is.True);
        Assert.That(storage.Remove(first), Is.False);
        Assert.That(storage.GetAll(), Is.EqualTo(new[] { second }));
    }

    private static IHost CreateHost(TimeSpan? timeout = null, IDarkWsBackplane? backplane = null) {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(services => {
                services.AddDarkWs(options => {
                    if (timeout.HasValue) {
                        options.BroadcastSendTimeout = timeout.Value;
                    }
                });
                if (backplane is not null) {
                    services.AddSingleton(backplane);
                }
            })
            .Build();
    }

    private sealed class RecordingBackplane : IDarkWsBackplane {
        private Func<BroadcastMessage, CancellationToken, ValueTask>? _listener;

        public ConcurrentQueue<BroadcastMessage> Published { get; } = new();

        public ValueTask PublishAsync(BroadcastMessage message, CancellationToken cancellationToken = default) {
            Published.Enqueue(message);
            return _listener?.Invoke(message, CancellationToken.None) ?? ValueTask.CompletedTask;
        }

        public ValueTask SubscribeAsync(Func<BroadcastMessage, CancellationToken, ValueTask> listener, CancellationToken cancellationToken = default) {
            _listener = listener;
            return ValueTask.CompletedTask;
        }

        public ValueTask UnsubscribeAsync(CancellationToken cancellationToken = default) {
            _listener = null;
            return ValueTask.CompletedTask;
        }
    }

    private static WebSocketConnection CreateConnection(TestWebSocket socket, string sessionId) {
        var user = new ClaimsPrincipal(
            new ClaimsIdentity([], "Test")
        );
        return new WebSocketConnection(socket, new DefaultHttpContext(), new TestSession(sessionId, user));
    }

    private static string? ReadAction(TestWebSocket socket) {
        using var message = JsonDocument.Parse(socket.Sent.Single());
        return message.RootElement.GetProperty("action").GetString();
    }
}
