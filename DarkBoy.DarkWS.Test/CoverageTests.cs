using System.Security.Claims;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using DarkBoy.DarkWS.Abstractions;
using DarkBoy.DarkWS.Test.Project;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NUnit.Framework;

namespace DarkBoy.DarkWS.Test;

public sealed class CoverageTests {
    [Test]
    public async Task DefaultAuthenticatorRejectsAnonymousPrincipalAsync() {
        var context = new DefaultHttpContext();
        var authenticator = new AspNetDarkWsAuthenticator();

        Assert.That(await authenticator.AuthenticateAsync(context, null, default), Is.Null);
    }

    [Test]
    public async Task DefaultAuthenticatorUsesSidClaimAsync() {
        var context = new DefaultHttpContext {
            User = Principal("user", new Claim("sid", "session-from-claim"))
        };
        var authenticator = new AspNetDarkWsAuthenticator();

        var session = await authenticator.AuthenticateAsync(context, null, default);

        Assert.That(session?.Id, Is.EqualTo("session-from-claim"));
        Assert.That(session?.User, Is.SameAs(context.User));
        Assert.That(session?.Groups, Is.Empty);
    }

    [Test]
    public async Task DefaultAuthenticatorUsesAspNetSessionIdWithoutSidClaimAsync() {
        var context = new DefaultHttpContext { User = Principal("user") };
        context.Features.Set<ISessionFeature>(new TestSessionFeature(new TestAspNetSession("asp-session")));

        var session = await new AspNetDarkWsAuthenticator().AuthenticateAsync(context, null, default);

        Assert.That(session?.Id, Is.EqualTo("asp-session"));
    }

    [Test]
    public async Task DefaultAuthenticatorGeneratesSessionIdAsLastFallbackAsync() {
        var context = new DefaultHttpContext { User = Principal("user") };

        var session = await new AspNetDarkWsAuthenticator().AuthenticateAsync(context, null, default);

        Assert.That(session?.Id, Has.Length.EqualTo(32));
    }

    [Test]
    public async Task InMemoryBackplaneStopsDeliveringAfterUnsubscribeAsync() {
        var backplane = new InMemoryDarkWsBackplane();
        var received = new List<DarkWsBroadcast>();
        await backplane.PublishAsync(new DarkWsBroadcast(DarkWsTarget.All, null, "before", null));
        await backplane.SubscribeAsync((message, _) => {
            received.Add(message);
            return ValueTask.CompletedTask;
        });
        await backplane.PublishAsync(new DarkWsBroadcast(DarkWsTarget.All, null, "during", null));
        await backplane.UnsubscribeAsync();
        await backplane.PublishAsync(new DarkWsBroadcast(DarkWsTarget.All, null, "after", null));

        Assert.That(received.Select(it => it.Action), Is.EqualTo(new[] { "during" }));
    }

    [Test]
    public async Task ConnectionReceivesFragmentedMessageAsync() {
        var socket = new TestWebSocket();
        socket.EnqueueReceive("hel", false);
        socket.EnqueueReceive("lo");
        using var connection = CreateConnection(socket);

        var message = await connection.ReceiveMessageAsync();

        Assert.That(Encoding.UTF8.GetString(message.Data), Is.EqualTo("hello"));
    }

    [Test]
    public async Task ClosingAlreadyClosedConnectionDoesNotCloseAgainAsync() {
        var socket = new TestWebSocket();
        socket.SetState(System.Net.WebSockets.WebSocketState.Closed);
        using var connection = CreateConnection(socket);

        await connection.CloseAsync();

        Assert.That(socket.State, Is.EqualTo(System.Net.WebSockets.WebSocketState.Closed));
    }

    private static WebSocketConnection CreateConnection(TestWebSocket socket) {
        return new WebSocketConnection(socket, new DefaultHttpContext(), null);
    }

    private static ClaimsPrincipal Principal(string name, params Claim[] claims) {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, name), .. claims],
            "Test"
        ));
    }

    private sealed class TestSessionFeature(ISession session) : ISessionFeature {
        public ISession Session { get; set; } = session;
    }

    private sealed class TestAspNetSession(string id) : ISession {
        private readonly Dictionary<string, byte[]> _values = new();
        public bool IsAvailable => true;
        public string Id { get; } = id;
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) {
            return _values.TryGetValue(key, out value);
        }
    }
}
