using System.Diagnostics;
using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;
using DarkWS;
using DarkWS.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var quick = args.Contains("--quick", StringComparer.Ordinal);
var samples = quick ? 3 : 5;
var iterations = quick ? 1 : 20;
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var payload = JsonSerializer.SerializeToElement(new { Text = new string('x', 256), Values = Enumerable.Range(0, 16).ToArray() });
var envelope = new ResponseMessage<object>(DarkWsProtocol.BroadcastId, new BroadcastActionMessage<JsonElement>("changed", payload));
var inputBytes = JsonSerializer.SerializeToUtf8Bytes(new InputMessage("request", "example:echo", payload), json);
long consumed = 0;

Console.WriteLine($"# {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; samples={samples}; iterations={iterations}; payload_bytes={inputBytes.Length}");
Console.WriteLine("scenario,recipients,median_ms_per_operation,median_allocated_bytes_per_operation");
foreach (var count in quick ? new[] { 100 } : new[] { 100, 1000, 10000 }) {
    await MeasureAsync("serialize-per-recipient", count, () => {
        for (var index = 0; index < count; index++) consumed += JsonSerializer.SerializeToUtf8Bytes(envelope, json).Length;
        return Task.CompletedTask;
    });
    await MeasureAsync("serialize-once", count, () => {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, json);
        for (var index = 0; index < count; index++) consumed += bytes.Length;
        return Task.CompletedTask;
    });

    using var host = new HostBuilder().ConfigureLogging(logging => logging.ClearProviders())
        .ConfigureServices(services => services.AddDarkWs()).Build();
    await host.StartAsync();
    var storage = host.Services.GetRequiredService<ConnectionStorage>();
    var connections = Enumerable.Range(0, count).Select(_ => new SinkConnection()).ToArray();
    foreach (var connection in connections) storage.Add(connection);
    var broadcaster = host.Services.GetRequiredService<IBroadcaster>();
    await MeasureAsync("in-memory-fanout", count, () => broadcaster.BroadcastAsync("changed", payload));
    if (connections.Any(connection => connection.Sends != 2 + samples * iterations)) throw new InvalidOperationException("Incomplete fanout");
    foreach (var connection in connections) { storage.Remove(connection); connection.Dispose(); }
    await host.StopAsync();
}

await MeasureAsync("parse-1000-requests", 1, () => {
    for (var index = 0; index < 1000; index++) {
        var message = JsonSerializer.Deserialize<InputMessage>(inputBytes, json) ?? throw new InvalidOperationException("Missing request");
        consumed += message.Id.Length + message.Action.Length;
    }
    return Task.CompletedTask;
});
GC.KeepAlive(consumed);

async Task MeasureAsync(string scenario, int recipients, Func<Task> operation) {
    // Warm-up is outside measurements; process-wide allocation counts include fanout workers.
    await operation();
    await operation();
    var times = new double[samples];
    var allocations = new double[samples];
    for (var sample = 0; sample < samples; sample++) {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        var started = Stopwatch.GetTimestamp();
        for (var iteration = 0; iteration < iterations; iteration++) await operation();
        times[sample] = Stopwatch.GetElapsedTime(started).TotalMilliseconds / iterations;
        allocations[sample] = (GC.GetTotalAllocatedBytes(precise: true) - allocated) / (double)iterations;
    }
    Array.Sort(times);
    Array.Sort(allocations);
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{scenario},{recipients},{times[samples / 2]:F4},{allocations[samples / 2]:F0}"));
}

sealed class SinkConnection : IWebSocketConnection {
    private int _sends;
    public int Sends => Volatile.Read(ref _sends);
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public WebSocket WebSocket => throw new NotSupportedException("Benchmark uses an in-memory sink");
    public HttpContext HttpContext { get; } = new DefaultHttpContext();
    public IDarkWsSession? Session => null;
    public bool IsOpen => true;
    public Task SendAsync(byte[] data, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        if (data.Length == 0) throw new InvalidOperationException("Empty broadcast");
        Interlocked.Increment(ref _sends);
        return Task.CompletedTask;
    }
    public Task<ReceivedMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Dispose() { }
}
