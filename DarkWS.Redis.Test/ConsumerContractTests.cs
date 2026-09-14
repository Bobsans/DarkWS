using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Claims;
using DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace DarkWS.Redis.Test;

// This assembly has no InternalsVisibleTo grant from DarkWS.
public sealed class ConsumerContractTests {
    [Test]
    public async Task ExternalConnectionImplementationWorksAndStorageReindexesReplacement() {
        var storage = new ConnectionStorage();
        using var first = new ConsumerConnection("same", new CountingSession("first", ["old", "old"]));
        storage.Add(first);
        await first.SendAsync([1, 2]);
        Assert.That(first.Sent, Is.EqualTo(new byte[] { 1, 2 }));
        first.Session = new CountingSession("second", ["new"]);
        storage.Add(first);
        Assert.That(storage.GetBySession("first"), Is.Empty);
        Assert.That(storage.GetByGroup("old"), Is.Empty);
        Assert.That(storage.GetBySession("second"), Is.EqualTo(new[] { first }));
        using var replacement = new ConsumerConnection("same", null);
        storage.Add(replacement);
        Assert.That(storage.Remove(first), Is.False);
        Assert.That(storage.GetByGroup("new"), Is.Empty);
        Assert.That(storage.GetAll(), Is.EqualTo(new[] { replacement }));
        Assert.That(storage.Remove(replacement), Is.True);
        Assert.That(storage.GetAll(), Is.Empty);
    }

    [Test]
    public void IndexedGroupLookupDoesNotInspectUnrelatedSessions() {
        var storage = new ConnectionStorage();
        var connections = Enumerable.Range(0, 10000).Select(index => new ConsumerConnection(index.ToString(),
            new CountingSession(index.ToString(), [index < 10 ? "small" : "other"]))).ToArray();
        foreach (var connection in connections) storage.Add(connection);
        foreach (var connection in connections) ((CountingSession)connection.Session!).Reads = 0;
        for (var index = 0; index < 100; index++) Assert.That(storage.GetByGroup("small"), Has.Count.EqualTo(10));
        Assert.That(connections.Sum(connection => ((CountingSession)connection.Session!).Reads), Is.Zero);

        // Comparative evidence only: machine speed is not a correctness assertion.
        const int iterations = 500;
        var timer = Stopwatch.StartNew();
        for (var index = 0; index < iterations; index++) _ = connections.Where(connection => connection.Session!.Groups.Contains("small")).ToArray();
        var scanTime = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        for (var index = 0; index < iterations; index++) _ = storage.GetByGroup("small");
        TestContext.Progress.WriteLine($"DW-016: 10000 connections / 10 recipients / {iterations} lookups: scan={scanTime:F2}ms, indexed={timer.Elapsed.TotalMilliseconds:F2}ms");
    }

    [Test]
    public void ConcurrentRegistrationAndRemovalKeepIndexesConsistent() {
        var storage = new ConnectionStorage();
        Parallel.For(0, 1000, index => {
            using var connection = new ConsumerConnection(index.ToString(), new CountingSession("shared", ["group"]));
            storage.Add(connection);
            Assert.That(storage.GetBySession("shared"), Does.Contain(connection));
            Assert.That(storage.GetByGroup("group"), Does.Contain(connection));
            storage.Remove(connection);
        });
        Assert.That(storage.GetAll(), Is.Empty);
        Assert.That(storage.GetBySession("shared"), Is.Empty);
        Assert.That(storage.GetByGroup("group"), Is.Empty);
    }

    private sealed class CountingSession(string id, string[] groups) : IDarkWsSession {
        public int Reads;
        public string Id => id;
        public ClaimsPrincipal User { get; } = new();
        public IReadOnlyCollection<string> Groups { get { Reads++; return groups; } }
    }

    private sealed class ConsumerConnection(string id, IDarkWsSession? session) : IWebSocketConnection {
        public string Id => id;
        public WebSocket WebSocket => throw new NotSupportedException("This test double has no transport");
        public HttpContext HttpContext { get; } = new DefaultHttpContext();
        public IDarkWsSession? Session { get; set; } = session;
        public bool IsOpen { get; private set; } = true;
        public byte[]? Sent { get; private set; }
        public Task<ReceivedMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendAsync(byte[] data, CancellationToken cancellationToken = default) { Sent = data; return Task.CompletedTask; }
        public Task CloseAsync(CancellationToken cancellationToken = default) { IsOpen = false; return Task.CompletedTask; }
        public void Dispose() => IsOpen = false;
    }
}
