#### [DarkWS\.Client](Overview.md 'Overview')
### [DarkWS\.Client](DarkWS.Client.md 'DarkWS\.Client')

## DarkWsClientOptions Class

Settings captured when a client is constructed\.

```csharp
public sealed class DarkWsClientOptions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → DarkWsClientOptions
### Properties

<a id='DarkWS.Client.DarkWsClientOptions.AuthenticationTokenProvider'></a>

## DarkWsClientOptions\.AuthenticationTokenProvider Property

Gets a nonempty token for acknowledged authentication on each connection\. Must honor cancellation\.

```csharp
public System.Func<System.Threading.CancellationToken,System.Threading.Tasks.ValueTask<string?>>? AuthenticationTokenProvider { get; set; }
```

#### Property Value
[System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[System\.Threading\.Tasks\.ValueTask&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1 'System\.Threading\.Tasks\.ValueTask\`1')[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1 'System\.Threading\.Tasks\.ValueTask\`1')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')

<a id='DarkWS.Client.DarkWsClientOptions.CloseTimeout'></a>

## DarkWsClientOptions\.CloseTimeout Property

Maximum graceful close duration\. Default: five seconds\.

```csharp
public System.TimeSpan CloseTimeout { get; set; }
```

#### Property Value
[System\.TimeSpan](https://learn.microsoft.com/en-us/dotnet/api/system.timespan 'System\.TimeSpan')

<a id='DarkWS.Client.DarkWsClientOptions.ConfigureWebSocketOptionsAsync'></a>

## DarkWsClientOptions\.ConfigureWebSocketOptionsAsync Property

Configures each fresh socket before connecting\. Must honor cancellation\.

```csharp
public System.Func<System.Net.WebSockets.ClientWebSocketOptions,System.Threading.CancellationToken,System.Threading.Tasks.ValueTask>? ConfigureWebSocketOptionsAsync { get; set; }
```

#### Property Value
[System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')[System\.Net\.WebSockets\.ClientWebSocketOptions](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.clientwebsocketoptions 'System\.Net\.WebSockets\.ClientWebSocketOptions')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')[System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')[System\.Threading\.Tasks\.ValueTask](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask 'System\.Threading\.Tasks\.ValueTask')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')

<a id='DarkWS.Client.DarkWsClientOptions.ConnectionTimeout'></a>

## DarkWsClientOptions\.ConnectionTimeout Property

Maximum connection attempt and connection wait duration\. Default: 30 seconds\.

```csharp
public System.TimeSpan ConnectionTimeout { get; set; }
```

#### Property Value
[System\.TimeSpan](https://learn.microsoft.com/en-us/dotnet/api/system.timespan 'System\.TimeSpan')

<a id='DarkWS.Client.DarkWsClientOptions.Endpoint'></a>

## DarkWsClientOptions\.Endpoint Property

Absolute ws or wss endpoint, without user information or a fragment\.

```csharp
public System.Uri Endpoint { get; set; }
```

#### Property Value
[System\.Uri](https://learn.microsoft.com/en-us/dotnet/api/system.uri 'System\.Uri')

<a id='DarkWS.Client.DarkWsClientOptions.JsonOptions'></a>

## DarkWsClientOptions\.JsonOptions Property

Payload and result serializer options\. The client takes a copy\. Default: Web defaults\.

```csharp
public System.Text.Json.JsonSerializerOptions JsonOptions { get; set; }
```

#### Property Value
[System\.Text\.Json\.JsonSerializerOptions](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializeroptions 'System\.Text\.Json\.JsonSerializerOptions')

<a id='DarkWS.Client.DarkWsClientOptions.MaxMessageSizeBytes'></a>

## DarkWsClientOptions\.MaxMessageSizeBytes Property

Maximum incoming message size, including all fragments\. Default: 1 MiB\.

```csharp
public int MaxMessageSizeBytes { get; set; }
```

#### Property Value
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

<a id='DarkWS.Client.DarkWsClientOptions.MaxPendingRequests'></a>

## DarkWsClientOptions\.MaxPendingRequests Property

Maximum application requests, including connection waits\. Default: 256\.

```csharp
public int MaxPendingRequests { get; set; }
```

#### Property Value
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

<a id='DarkWS.Client.DarkWsClientOptions.NotificationQueueCapacity'></a>

## DarkWsClientOptions\.NotificationQueueCapacity Property

Maximum queued broadcasts\. Default: 256\.

```csharp
public int NotificationQueueCapacity { get; set; }
```

#### Property Value
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

<a id='DarkWS.Client.DarkWsClientOptions.PingInterval'></a>

## DarkWsClientOptions\.PingInterval Property

Interval before sending a text ping\. Default: 30 seconds\.

```csharp
public System.TimeSpan PingInterval { get; set; }
```

#### Property Value
[System\.TimeSpan](https://learn.microsoft.com/en-us/dotnet/api/system.timespan 'System\.TimeSpan')

<a id='DarkWS.Client.DarkWsClientOptions.PongTimeout'></a>

## DarkWsClientOptions\.PongTimeout Property

Maximum pong wait after writing a ping\. Default: 30 seconds\.

```csharp
public System.TimeSpan PongTimeout { get; set; }
```

#### Property Value
[System\.TimeSpan](https://learn.microsoft.com/en-us/dotnet/api/system.timespan 'System\.TimeSpan')

<a id='DarkWS.Client.DarkWsClientOptions.Reconnect'></a>

## DarkWsClientOptions\.Reconnect Property

Whether transport failures trigger reconnect\. Default: true\.

```csharp
public bool Reconnect { get; set; }
```

#### Property Value
[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')

<a id='DarkWS.Client.DarkWsClientOptions.RequestTimeout'></a>

## DarkWsClientOptions\.RequestTimeout Property

Response timeout after sending\. Default: five minutes; InfiniteTimeSpan disables expiry\.

```csharp
public System.TimeSpan RequestTimeout { get; set; }
```

#### Property Value
[System\.TimeSpan](https://learn.microsoft.com/en-us/dotnet/api/system.timespan 'System\.TimeSpan')

<a id='DarkWS.Client.DarkWsClientOptions.SendTimeout'></a>

## DarkWsClientOptions\.SendTimeout Property

Maximum send queue and write duration\. Default: 30 seconds\.

```csharp
public System.TimeSpan SendTimeout { get; set; }
```

#### Property Value
[System\.TimeSpan](https://learn.microsoft.com/en-us/dotnet/api/system.timespan 'System\.TimeSpan')