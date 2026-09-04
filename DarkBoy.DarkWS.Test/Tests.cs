using DarkBoy.DarkWS.Test.Project;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System.Net.WebSockets;

namespace DarkBoy.DarkWS.Test;

public sealed class Tests {
    private IHost Host { get; set; } = null!;
    private TestServer Server => Host.GetTestServer();

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync() {
        Host = await Setup.CreateBuilder().StartAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync() {
        await Host.StopAsync();
        Host.Dispose();
    }

    [Test]
    public async Task AnonymousActionRunsWithoutSessionAsync() {
        using var webSocket = await ConnectAsync();
        await webSocket.SendMessage(new RequestMessage("0", "public:get"));

        Assert.That(
            await webSocket.ReceiveMessage<ResponseMessage<string>>(),
            Is.EqualTo(new ResponseMessage<string>("0", "public result"))
        );
    }

    [Test]
    public async Task ActionRequiresAuthenticationByDefaultAsync() {
        using var webSocket = await ConnectAsync();
        await webSocket.SendMessage(new RequestMessage("1", "test:get"));

        Assert.That(
            await webSocket.ReceiveMessage<ErrorMessage>(),
            Is.EqualTo(new ErrorMessage("1", "darkws:error:authorization-required"))
        );
    }

    [Test]
    public async Task TypedAndAspNetSessionsAreAvailableAsync() {
        using var webSocket = await ConnectAsync("first");
        await webSocket.SendMessage(new RequestMessage("2", "test:session"));

        var message = await webSocket.ReceiveMessage<ResponseMessage<TestHandler.SessionResult>>();
        Assert.That(message?.Data, Is.EqualTo(new TestHandler.SessionResult("first", "first", "first", true)));
    }

    [Test]
    public async Task AuthMessageReplacesSessionAsync() {
        using var webSocket = await ConnectAsync("first");
        await webSocket.SendTextAsync("auth:second");
        await webSocket.SendMessage(new RequestMessage("3", "test:session"));

        var message = await webSocket.ReceiveMessage<ResponseMessage<TestHandler.SessionResult>>();
        Assert.That(message?.Data?.Handler, Is.EqualTo("second"));
    }

    [Test]
    public async Task EachMessageUsesANewScopeAsync() {
        using var webSocket = await ConnectAsync("first");
        await webSocket.SendMessage(new RequestMessage("4", "test:scope"));
        await webSocket.SendMessage(new RequestMessage("5", "test:scope"));

        var first = await webSocket.ReceiveMessage<ResponseMessage<Guid>>();
        var second = await webSocket.ReceiveMessage<ResponseMessage<Guid>>();
        Assert.That(second?.Data, Is.Not.EqualTo(first?.Data));
    }

    [Test]
    public async Task UnhandledErrorDoesNotLeakExceptionTextAsync() {
        using var webSocket = await ConnectAsync("first");
        await webSocket.SendMessage(new RequestMessage("6", "test:fail"));

        Assert.That(
            await webSocket.ReceiveMessage<ErrorMessage>(),
            Is.EqualTo(new ErrorMessage("6", "darkws:error:request-failed"))
        );
    }

    [Test]
    public async Task ControlledErrorIncludesTypedDetailsAsync() {
        using var webSocket = await ConnectAsync("first");
        await webSocket.SendMessage(new RequestMessage("controlled", "test:controlled-error"));

        Assert.That(
            await webSocket.ReceiveMessage<ErrorMessage<int>>(),
            Is.EqualTo(new ErrorMessage<int>("controlled", "rejected", 42))
        );
    }

    [Test]
    public async Task InvalidActionReturnsStableErrorAsync() {
        using var webSocket = await ConnectAsync("first");
        await webSocket.SendMessage(new RequestMessage("invalid", "missing:action"));

        Assert.That(
            await webSocket.ReceiveMessage<ErrorMessage>(),
            Is.EqualTo(new ErrorMessage("invalid", "darkws:error:invalid-action"))
        );
    }

    [Test]
    public async Task InvalidRequestWithIdReturnsStableErrorAsync() {
        using var webSocket = await ConnectAsync("first");
        await webSocket.SendTextAsync("{\"id\":\"broken\",\"action\":\"\"}");

        Assert.That(
            await webSocket.ReceiveMessage<ErrorMessage>(),
            Is.EqualTo(new ErrorMessage("broken", "darkws:error:invalid-request"))
        );
    }

    [Test]
    public async Task MalformedJsonDoesNotCloseConnectionAsync() {
        using var webSocket = await ConnectAsync("first");
        await webSocket.SendTextAsync("{");
        await webSocket.SendTextAsync("ping");

        Assert.That(System.Text.Encoding.UTF8.GetString(await webSocket.ReceiveRawMessage()), Is.EqualTo("pong"));
    }

    [Test]
    public async Task PingReturnsPongAsync() {
        using var webSocket = await ConnectAsync();
        await webSocket.SendTextAsync("ping");

        Assert.That(System.Text.Encoding.UTF8.GetString(await webSocket.ReceiveRawMessage()), Is.EqualTo("pong"));
    }

    [Test]
    public async Task NonWebSocketRequestReturnsBadRequestAsync() {
        using var response = await Server.CreateClient().GetAsync("/ws");
        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task LifecycleAndScopeHooksRunAsync() {
        var probe = Host.Services.GetRequiredService<LifecycleProbe>();
        var openBefore = probe.OpenCount;
        var authBefore = probe.AuthenticationCount;
        var closeBefore = probe.CloseCount;
        var scopeBefore = probe.ScopeInitializationCount;
        var webSocket = await ConnectAsync("first");
        await webSocket.SendTextAsync("auth:second");
        await webSocket.SendMessage(new RequestMessage("hook", "test:get"));
        _ = await webSocket.ReceiveMessage<ResponseMessage<string>>();
        webSocket.Abort();

        await WaitUntilAsync(() => probe.CloseCount > closeBefore);
        Assert.Multiple(() => {
            Assert.That(probe.OpenCount, Is.EqualTo(openBefore + 1));
            Assert.That(probe.AuthenticationCount, Is.EqualTo(authBefore + 1));
            Assert.That(probe.PreviousSessionId, Is.EqualTo("first"));
            Assert.That(probe.ScopeInitializationCount, Is.EqualTo(scopeBefore + 1));
        });
        webSocket.Dispose();
    }

    [Test]
    public void DuplicateActionRegistrationFailsAtStartup() {
        var services = new ServiceCollection();
        var builder = services.AddDarkWs().AddHandlersFromAssemblyContaining<TestHandler>();

        Assert.Throws<InvalidOperationException>(() => builder.AddHandlersFromAssemblyContaining<TestHandler>());
    }

    [Test]
    public async Task BroadcastToSelfUsesBroadcastWireContractAsync() {
        using var webSocket = await ConnectAsync("first");
        await webSocket.SendMessage(new RequestMessage<string>("7", "test:broadcast", "hello"));

        var broadcast = await webSocket.ReceiveMessage<ResponseMessage<BroadcastActionMessage>>();
        Assert.That(
            broadcast,
            Is.EqualTo(new ResponseMessage<BroadcastActionMessage>("@", new BroadcastActionMessage("hello")))
        );
        Assert.That(
            await webSocket.ReceiveMessage<ResponseMessageNoData>(),
            Is.EqualTo(new ResponseMessageNoData("7"))
        );
    }

    private Task<WebSocket> ConnectAsync(string? token = null) {
        var uri = token is null ? new Uri("ws://localhost/ws") : new Uri($"ws://localhost/ws?token={token}");
        return Server.CreateWebSocketClient().ConnectAsync(uri, CancellationToken.None);
    }

    private static async Task WaitUntilAsync(Func<bool> condition) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition()) {
            await Task.Delay(10, timeout.Token);
        }
    }
}
