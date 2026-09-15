using DarkWS.Client;

await using var client = new DarkWsClient(new Uri("ws://localhost/ws"));
using var subscription = client.On<int>("changed", Console.WriteLine);
if (client.State != DarkWsClientState.Disconnected)
    throw new InvalidOperationException("Construction must not connect.");
var runtimeConfiguration = File.ReadAllText(Path.ChangeExtension(typeof(Program).Assembly.Location, ".runtimeconfig.json"));
if (runtimeConfiguration.Contains("Microsoft.AspNetCore.App", StringComparison.Ordinal))
    throw new InvalidOperationException("The core client must not require ASP.NET.");
Console.WriteLine($"Standalone client consumer passed on {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
