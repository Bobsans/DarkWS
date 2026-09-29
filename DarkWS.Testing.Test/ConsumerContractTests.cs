using System.Diagnostics;
using System.Security.Claims;
using DarkWS.Abstractions;
using DarkWS.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DarkWS.Testing.Test;

// This assembly has no InternalsVisibleTo grant from DarkWS or DarkWS.Testing.
public sealed class ConsumerContractTests {
    [Test]
    public async Task RefreshReindexesChangedMembershipOfRegisteredConnectionsOnly() {
        await using var host = new DarkWsTestHost(_ => { });
        var session = new MutableSession("user", ["old", "old"]);
        var connection = host.CreateConnection(session);
        var connections = host.Services.GetRequiredService<IDarkWsConnections>();
        Assert.That(connections.Find(connection.Id), Is.SameAs(connection));
        Assert.That(connections.GetByGroup("old"), Is.EqualTo(new[] { connection }));

        session.Groups = ["new"];
        Assert.That(connections.GetByGroup("new"), Is.Empty);
        Assert.That(connections.Refresh(connection), Is.True);
        Assert.That(connections.GetByGroup("old"), Is.Empty);
        Assert.That(connections.GetByGroup("new"), Is.EqualTo(new[] { connection }));
        Assert.That(connections.GetBySession("user"), Is.EqualTo(new[] { connection }));

        using var unregistered = new DarkWsTestConnection(session);
        Assert.That(connections.Refresh(unregistered), Is.False);
        Assert.That(connections.Find(unregistered.Id), Is.Null);
        connection.Dispose();
        Assert.That(connections.Refresh(connection), Is.False);
    }

    [Test]
    public async Task IndexedGroupLookupDoesNotInspectUnrelatedSessions() {
        await using var host = new DarkWsTestHost(_ => { });
        var sessions = Enumerable.Range(0, 10000)
            .Select(index => new CountingSession(index.ToString(), [index < 10 ? "small" : "other"])).ToArray();
        foreach (var session in sessions) {
            host.CreateConnection(session);
        }

        var connections = host.Services.GetRequiredService<IDarkWsConnections>();
        foreach (var session in sessions) {
            session.Reads = 0;
        }

        for (var index = 0; index < 100; index++) {
            Assert.That(connections.GetByGroup("small"), Has.Count.EqualTo(10));
        }

        Assert.That(sessions.Sum(session => session.Reads), Is.Zero);

        // Comparative evidence only: machine speed is not a correctness assertion.
        const int iterations = 500;
        var timer = Stopwatch.StartNew();
        for (var index = 0; index < iterations; index++) {
            _ = sessions.Where(session => session.Groups.Contains("small")).ToArray();
        }

        var scanTime = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        for (var index = 0; index < iterations; index++) {
            _ = connections.GetByGroup("small");
        }

        TestContext.Progress.WriteLine($"DW-016: 10000 connections / 10 recipients / {iterations} lookups: scan={scanTime:F2}ms, indexed={timer.Elapsed.TotalMilliseconds:F2}ms");
    }

    private sealed class MutableSession(string id, IReadOnlyCollection<string> groups) : IDarkWsSession {
        public string Id => id;
        public ClaimsPrincipal User { get; } = new();
        public IReadOnlyCollection<string> Groups { get; set; } = groups;
    }

    private sealed class CountingSession(string id, string[] groups) : IDarkWsSession {
        public int Reads;
        public string Id => id;
        public ClaimsPrincipal User { get; } = new();

        public IReadOnlyCollection<string> Groups {
            get {
                Reads++;
                return groups;
            }
        }
    }
}
