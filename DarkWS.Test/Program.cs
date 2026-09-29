using DarkWS.Test.Project;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DarkWS.Test;

public static class Setup {
    public static IHostBuilder CreateBuilder(Action<IServiceCollection, DarkWsBuilder>? configureDarkWs = null) {
        return new HostBuilder().ConfigureWebHost(webBuilder => webBuilder
            .UseTestServer()
            .ConfigureServices(services => {
                services.AddRouting();
                services.AddDistributedMemoryCache();
                services.AddSession();
                services.AddScoped<ScopedProbe>();
                services.AddSingleton<LifecycleProbe>();
                var darkWs = services.AddDarkWs()
                    .AddHandlersFromAssemblyContaining<TestHandler>()
                    .AddAuthenticator<TestAuthenticator, TestSession>()
                    .AddScopeInitializer<TestScopeInitializer>()
                    .AddConnectionHooks<TestConnectionHooks>();
                configureDarkWs?.Invoke(services, darkWs);
            })
            .Configure(app => {
                app.UseRouting();
                app.UseSession();
                app.UseWebSockets();
                app.UseEndpoints(endpoints => endpoints.MapDarkWs("/ws"));
            }));
    }
}
