using System.Net.WebSockets;
using DarkWS;
using DarkWS.Abstractions;
using DarkWS.Redis;
using DarkWS.Client;
using DarkWS.Testing;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

var services = new ServiceCollection();
await using (var testing = new DarkWsTestHost(darkWs => darkWs.AddHandlersFromAssemblyContaining<PackageHandler>())) {
    var fake = testing.CreateConnection();
    await testing.InvokeAsync(fake, "package:echo", JsonSerializer.SerializeToElement("testing"), requestId: "test");
    using var response = JsonDocument.Parse(fake.SentMessages.Single());
    if (response.RootElement.GetProperty("data").GetString() != "testing")
        throw new InvalidOperationException("The installed testing package failed a handler request.");
    await using var scope = testing.CreateScope(fake);
    var handler = new PackageHandler();
    scope.Initialize(handler);
    await handler.Echo("direct").WriteResultAsync(new ResponseContext(fake, "direct", new DarkWsOptions()));
    if (fake.SentMessages.Count != 2)
        throw new InvalidOperationException("The installed testing package failed direct handler initialization.");
    var sender = testing.CreateConnection(new SmokeGroupSession());
    var recipient = testing.CreateConnection(new SmokeGroupSession());
    await testing.Services.GetRequiredService<IBroadcaster>().BroadcastToGroupsAsync(
        ["a", "b", "a"], "changed", except: new DarkWsBroadcastExclusion { ConnectionId = sender.Id });
    if (sender.SentMessages.Count != 0 || recipient.SentMessages.Count != 1 || testing.Broadcasts.Count != 1)
        throw new InvalidOperationException("The installed package failed group union or exclusion delivery.");
}
services.AddDarkWs(options => options.MaxMessageSizeBytes = 65536)
    .AddHandlersFromAssemblyContaining<PackageHandler>();
services.Configure<DarkWsOptions>(options => options.MaxMessageSizeBytes = 777);
services.AddDarkWsRedis("package-smoke");
using var provider = services.BuildServiceProvider();
if (provider.GetRequiredService<IOptions<DarkWsOptions>>().Value.MaxMessageSizeBytes != 777)
    throw new InvalidOperationException("The installed package ignored Configure<DarkWsOptions>");
try {
    services.AddDarkWs();
    throw new Exception("The installed package allowed duplicate registration");
} catch (InvalidOperationException) { }
using IWebSocketConnection connection = new ConsumerConnection();
await connection.SendAsync([1]);
if (new ErrorResponseException("domain:error").Message != "domain:error")
    throw new InvalidOperationException("The installed package lost the domain error message");
var builder = WebApplication.CreateBuilder();
builder.WebHost.ConfigureKestrel(options => options.Listen(System.Net.IPAddress.Loopback, 0));
builder.Services.AddDarkWs().AddHandlersFromAssemblyContaining<PackageHandler>();
await using var app = builder.Build();
app.UseWebSockets();
app.MapDarkWs("/ws");
await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
var endpoint = new Uri(address.Replace("http://", "ws://", StringComparison.Ordinal) + "/ws");
await using (var client = new DarkWsClient(endpoint)) {
    if (await client.RequestAsync<string>("package:echo", "direct") != "direct")
        throw new InvalidOperationException("The installed client failed a real DarkWS request.");
}
var clientServices = new ServiceCollection();
clientServices.AddDarkWsClient(options => options.Endpoint = endpoint);
await using (var clientProvider = clientServices.BuildServiceProvider()) {
    var client = clientProvider.GetRequiredService<IDarkWsClient>();
    if (await client.RequestAsync<string>("package:echo", "injected") != "injected")
        throw new InvalidOperationException("The installed DI client failed a real DarkWS request.");
}
await app.StopAsync();
Console.WriteLine($"Installed package smoke passed on {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; Redis dependency {typeof(StackExchange.Redis.IConnectionMultiplexer).Assembly.GetName().Version}");

[Handler("package"), AllowAnonymous]
public sealed class PackageHandler : HandlerBase {
    [Action("echo")]
    public IResponse Echo(string input) => Ok(input);
}

public sealed class ConsumerConnection : IWebSocketConnection {
    public string Id => "consumer";
    public WebSocket WebSocket => throw new NotSupportedException("No transport in this test double");
    public HttpContext HttpContext { get; } = new DefaultHttpContext();
    public IDarkWsSession? Session => null;
    public bool IsOpen => true;
    public Task<ReceivedMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SendAsync(byte[] data, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Dispose() { }
}

public sealed class SmokeGroupSession : IDarkWsSession {
    public string Id => "smoke";
    public IReadOnlyCollection<string> Groups => ["a", "b"];
    public System.Security.Claims.ClaimsPrincipal User { get; } = new();
}
