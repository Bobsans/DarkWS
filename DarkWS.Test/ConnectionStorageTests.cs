using System.Security.Claims;
using DarkWS.Abstractions;
using DarkWS.Test.Project;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace DarkWS.Test;

public sealed class ConnectionStorageTests {
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task SlowGroupsDoNotBlockLookupsOrReregisterClosedConnections(bool setSession, bool close) {
        var storage = new ConnectionStorage();
        using var entered = new SemaphoreSlim(0, 1);
        using var release = new SemaphoreSlim(0, 1);
        var previous = new Session("old", () => ["old-group"]);
        var next = new Session("new", () => {
            entered.Release();
            if (!release.Wait(TimeSpan.FromSeconds(10))) {
                throw new TimeoutException("Groups were not released");
            }
            return ["new-group", "new-group"];
        });
        using var connection = new WebSocketConnection(new TestWebSocket(), new DefaultHttpContext(), setSession ? previous : next);
        if (setSession) {
            storage.Add(connection);
        }

        var update = Task.Run(() => {
            if (setSession) {
                storage.SetSession(connection, next);
            } else {
                storage.Add(connection);
            }
        });
        try {
            Assert.That(await entered.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
            var lookup = await Task.Run(storage.GetAll).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.That(lookup, Has.Count.EqualTo(setSession ? 1 : 0));
            if (setSession) {
                Assert.That(connection.Session, Is.SameAs(previous));
                Assert.That(storage.GetByGroup("old-group"), Is.EqualTo(new[] { connection }));
            }
            if (close) {
                connection.BeginClosing();
                storage.Remove(connection);
            }
        } finally {
            release.Release();
            await update.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.That(storage.GetBySession("old"), Is.Empty);
        Assert.That(storage.GetByGroup("old-group"), Is.Empty);
        Assert.That(storage.GetAll(), Has.Count.EqualTo(close ? 0 : 1));
        Assert.That(storage.GetBySession("new"), Has.Count.EqualTo(close ? 0 : 1));
        Assert.That(storage.GetByGroup("new-group"), Has.Count.EqualTo(close ? 0 : 1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ThrowingGroupsPreserveSessionAndIndexes(bool setSession) {
        var storage = new ConnectionStorage();
        var fail = false;
        var previous = new Session("old", () => fail ? throw new InvalidOperationException("Groups failed") : ["old-group"]);
        var next = new Session("new", () => throw new InvalidOperationException("Groups failed"));
        using var connection = new WebSocketConnection(new TestWebSocket(), new DefaultHttpContext(), previous);
        storage.Add(connection);
        fail = true;

        Assert.Throws<InvalidOperationException>(() => {
            if (setSession) {
                storage.SetSession(connection, next);
            } else {
                storage.Add(connection);
            }
        });

        Assert.That(connection.Session, Is.SameAs(previous));
        Assert.That(storage.GetBySession("old"), Is.EqualTo(new[] { connection }));
        Assert.That(storage.GetByGroup("old-group"), Is.EqualTo(new[] { connection }));
        Assert.That(storage.GetBySession("new"), Is.Empty);
    }

    [Test]
    public async Task RefreshDoesNotOverwriteConcurrentSessionChange() {
        var storage = new ConnectionStorage();
        using var entered = new SemaphoreSlim(0, 1);
        using var release = new SemaphoreSlim(0, 1);
        var block = false;
        var previous = new Session("old", () => {
            if (block) {
                entered.Release();
                if (!release.Wait(TimeSpan.FromSeconds(10))) {
                    throw new TimeoutException("Groups were not released");
                }
            }
            return ["old-group"];
        });
        using var connection = new WebSocketConnection(new TestWebSocket(), new DefaultHttpContext(), previous);
        storage.Add(connection);
        block = true;
        var refresh = Task.Run(() => storage.Add(connection));
        var next = new Session("new", () => ["new-group"]);
        try {
            Assert.That(await entered.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
            await Task.Run(() => storage.SetSession(connection, next)).WaitAsync(TimeSpan.FromSeconds(2));
        } finally {
            release.Release();
            await refresh.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.That(connection.Session, Is.SameAs(next));
        Assert.That(storage.GetBySession("old"), Is.Empty);
        Assert.That(storage.GetByGroup("old-group"), Is.Empty);
        Assert.That(storage.GetBySession("new"), Is.EqualTo(new[] { connection }));
        Assert.That(storage.GetByGroup("new-group"), Is.EqualTo(new[] { connection }));
    }

    private sealed class Session(string id, Func<IReadOnlyCollection<string>> groups) : IDarkWsSession {
        public string Id => id;
        public ClaimsPrincipal User { get; } = new(new ClaimsIdentity());
        public IReadOnlyCollection<string> Groups => groups();
    }
}
