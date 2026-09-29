#### [DarkWS](Overview.md 'Overview')
### [DarkWS\.Abstractions](DarkWS.Abstractions.md 'DarkWS\.Abstractions')

## IWebSocketConnection Interface

Public connection contract implementable by consumers and test doubles\. Send buffers must not be mutated\.

```csharp
public interface IWebSocketConnection : System.IDisposable
```

Derived  
↳ [WebSocketConnection](DarkWS.WebSocketConnection.md 'DarkWS\.WebSocketConnection')

Implements [System\.IDisposable](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable 'System\.IDisposable')
### Properties

<a id='DarkWS.Abstractions.IWebSocketConnection.HttpContext'></a>

## IWebSocketConnection\.HttpContext Property

Gets the HTTP upgrade context shared by the connection\.

```csharp
Microsoft.AspNetCore.Http.HttpContext HttpContext { get; }
```

#### Property Value
[Microsoft\.AspNetCore\.Http\.HttpContext](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httpcontext 'Microsoft\.AspNetCore\.Http\.HttpContext')

<a id='DarkWS.Abstractions.IWebSocketConnection.Id'></a>

## IWebSocketConnection\.Id Property

Gets the stable identity for connection or session targeting\.

```csharp
string Id { get; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IWebSocketConnection.IsOpen'></a>

## IWebSocketConnection\.IsOpen Property

Reports whether the transport is open and has not begun closing\.

```csharp
bool IsOpen { get; }
```

#### Property Value
[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')

<a id='DarkWS.Abstractions.IWebSocketConnection.Session'></a>

## IWebSocketConnection\.Session Property

Gets the current session, or null for anonymous access\.

```csharp
DarkWS.Abstractions.IDarkWsSession? Session { get; }
```

#### Property Value
[IDarkWsSession](DarkWS.Abstractions.IDarkWsSession.md 'DarkWS\.Abstractions\.IDarkWsSession')

<a id='DarkWS.Abstractions.IWebSocketConnection.WebSocket'></a>

## IWebSocketConnection\.WebSocket Property

Gets the owned transport\. Use connection methods to preserve write serialization and disposal coordination\.

```csharp
System.Net.WebSockets.WebSocket WebSocket { get; }
```

#### Property Value
[System\.Net\.WebSockets\.WebSocket](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.websocket 'System\.Net\.WebSockets\.WebSocket')
### Methods

<a id='DarkWS.Abstractions.IWebSocketConnection.CloseAsync(System.Threading.CancellationToken)'></a>

## IWebSocketConnection\.CloseAsync\(CancellationToken\) Method

Stops new writes and performs graceful close\. Supply cancellation to bound the close handshake\.

```csharp
System.Threading.Tasks.Task CloseAsync(System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Abstractions.IWebSocketConnection.CloseAsync(System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.Abstractions.IWebSocketConnection.ReceiveMessageAsync(System.Threading.CancellationToken)'></a>

## IWebSocketConnection\.ReceiveMessageAsync\(CancellationToken\) Method

Receives a complete message\. Close frames discard partial data; exceeding the size limit closes with status 1009\.

```csharp
System.Threading.Tasks.Task<DarkWS.ReceivedMessage> ReceiveMessageAsync(System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Abstractions.IWebSocketConnection.ReceiveMessageAsync(System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1 'System\.Threading\.Tasks\.Task\`1')[ReceivedMessage](DarkWS.ReceivedMessage.md 'DarkWS\.ReceivedMessage')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1 'System\.Threading\.Tasks\.Task\`1')

<a id='DarkWS.Abstractions.IWebSocketConnection.SendAsync(byte[],System.Threading.CancellationToken)'></a>

## IWebSocketConnection\.SendAsync\(byte\[\], CancellationToken\) Method

Sends immutable bytes with serialized writes\. The built\-in transport bounds lock wait and send time and aborts on timeout; closing connections ignore new writes\.

```csharp
System.Threading.Tasks.Task SendAsync(byte[] data, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Abstractions.IWebSocketConnection.SendAsync(byte[],System.Threading.CancellationToken).data'></a>

`data` [System\.Byte](https://learn.microsoft.com/en-us/dotnet/api/system.byte 'System\.Byte')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

<a id='DarkWS.Abstractions.IWebSocketConnection.SendAsync(byte[],System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')