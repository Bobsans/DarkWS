using System.Net;
using System.Net.WebSockets;
using System.Text;
using DarkWS.Test.Project;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace DarkWS.Test;

public sealed class CompressionTests {
    [TestCase(true)]
    [TestCase(false)]
    public async Task CompressionIsNegotiatedOnlyWhenEnabled(bool enabled) {
        using var host = await new HostBuilder().ConfigureWebHost(builder => builder
            .UseKestrel(options => options.Listen(IPAddress.Loopback, 0))
            .ConfigureServices(services => {
                services.AddRouting();
                services.AddScoped<PublicHandler>();
                services.AddDarkWs(options => options.EnableCompression = enabled);
            })
            .Configure(app => {
                app.UseRouting();
                app.UseWebSockets();
                app.UseEndpoints(endpoints => endpoints.MapDarkWs());
            })).StartAsync();
        host.Services.GetRequiredService<DarkWsActionRegistry>().Add(typeof(PublicHandler));
        var address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var socket = new ClientWebSocket();
        socket.Options.CollectHttpResponseDetails = true;
        socket.Options.DangerousDeflateOptions = new WebSocketDeflateOptions();
        await socket.ConnectAsync(new Uri(address.Replace("http://", "ws://", StringComparison.Ordinal) + "/ws"), CancellationToken.None);

        var extensions = socket.HttpResponseHeaders?.GetValueOrDefault("Sec-WebSocket-Extensions")?.SingleOrDefault();
        if (enabled) {
            Assert.That(extensions, Does.StartWith("permessage-deflate").And.Contain("server_no_context_takeover"));
        } else {
            Assert.That(extensions, Is.Null);
        }

        await socket.SendTextAsync("""{"id":"1","action":"public:get"}""");
        Assert.That(Encoding.UTF8.GetString(await socket.ReceiveRawMessage()), Is.EqualTo("""{"id":"1","data":"public result"}"""));
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        await host.StopAsync();
    }
}
