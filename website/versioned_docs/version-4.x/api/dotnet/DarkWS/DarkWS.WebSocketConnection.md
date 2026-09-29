#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## WebSocketConnection Class

Owns a WebSocket with serialized, bounded writes and coordinated disposal\. Send buffers are immutable\.

```csharp
public sealed class WebSocketConnection : DarkWS.Abstractions.IWebSocketConnection, System.IDisposable
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → WebSocketConnection

Implements [IWebSocketConnection](DarkWS.Abstractions.IWebSocketConnection.md 'DarkWS\.Abstractions\.IWebSocketConnection'), [System\.IDisposable](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable 'System\.IDisposable')
### Constructors

<a id='DarkWS.WebSocketConnection.WebSocketConnection(System.Net.WebSockets.WebSocket,Microsoft.AspNetCore.Http.HttpContext,DarkWS.Abstractions.IDarkWsSession)'></a>

## WebSocketConnection\(WebSocket, HttpContext, IDarkWsSession\) Constructor

Creates a connection with a 1 MiB incoming limit and a 30\-second send timeout\.

```csharp
public WebSocketConnection(System.Net.WebSockets.WebSocket webSocket, Microsoft.AspNetCore.Http.HttpContext context, DarkWS.Abstractions.IDarkWsSession? session);
```
#### Parameters

<a id='DarkWS.WebSocketConnection.WebSocketConnection(System.Net.WebSockets.WebSocket,Microsoft.AspNetCore.Http.HttpContext,DarkWS.Abstractions.IDarkWsSession).webSocket'></a>

`webSocket` [System\.Net\.WebSockets\.WebSocket](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.websocket 'System\.Net\.WebSockets\.WebSocket')

<a id='DarkWS.WebSocketConnection.WebSocketConnection(System.Net.WebSockets.WebSocket,Microsoft.AspNetCore.Http.HttpContext,DarkWS.Abstractions.IDarkWsSession).context'></a>

`context` [Microsoft\.AspNetCore\.Http\.HttpContext](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httpcontext 'Microsoft\.AspNetCore\.Http\.HttpContext')

<a id='DarkWS.WebSocketConnection.WebSocketConnection(System.Net.WebSockets.WebSocket,Microsoft.AspNetCore.Http.HttpContext,DarkWS.Abstractions.IDarkWsSession).session'></a>

`session` [IDarkWsSession](DarkWS.Abstractions.IDarkWsSession.md 'DarkWS\.Abstractions\.IDarkWsSession')

<a id='DarkWS.WebSocketConnection.WebSocketConnection(System.Net.WebSockets.WebSocket,Microsoft.AspNetCore.Http.HttpContext,DarkWS.Abstractions.IDarkWsSession,int)'></a>

## WebSocketConnection\(WebSocket, HttpContext, IDarkWsSession, int\) Constructor

Owns a WebSocket with serialized, bounded writes and coordinated disposal\. Send buffers are immutable\.

```csharp
public WebSocketConnection(System.Net.WebSockets.WebSocket webSocket, Microsoft.AspNetCore.Http.HttpContext context, DarkWS.Abstractions.IDarkWsSession? session, int maxMessageSizeBytes);
```
#### Parameters

<a id='DarkWS.WebSocketConnection.WebSocketConnection(System.Net.WebSockets.WebSocket,Microsoft.AspNetCore.Http.HttpContext,DarkWS.Abstractions.IDarkWsSession,int).webSocket'></a>

`webSocket` [System\.Net\.WebSockets\.WebSocket](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.websocket 'System\.Net\.WebSockets\.WebSocket')

Owned WebSocket transport\.

<a id='DarkWS.WebSocketConnection.WebSocketConnection(System.Net.WebSockets.WebSocket,Microsoft.AspNetCore.Http.HttpContext,DarkWS.Abstractions.IDarkWsSession,int).context'></a>

`context` [Microsoft\.AspNetCore\.Http\.HttpContext](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httpcontext 'Microsoft\.AspNetCore\.Http\.HttpContext')

HTTP upgrade context\.

<a id='DarkWS.WebSocketConnection.WebSocketConnection(System.Net.WebSockets.WebSocket,Microsoft.AspNetCore.Http.HttpContext,DarkWS.Abstractions.IDarkWsSession,int).session'></a>

`session` [IDarkWsSession](DarkWS.Abstractions.IDarkWsSession.md 'DarkWS\.Abstractions\.IDarkWsSession')

Initial session, or null for anonymous access\.

<a id='DarkWS.WebSocketConnection.WebSocketConnection(System.Net.WebSockets.WebSocket,Microsoft.AspNetCore.Http.HttpContext,DarkWS.Abstractions.IDarkWsSession,int).maxMessageSizeBytes'></a>

`maxMessageSizeBytes` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

Positive complete\-message size limit in bytes\.
### Properties

<a id='DarkWS.WebSocketConnection.HttpContext'></a>

## WebSocketConnection\.HttpContext Property

Gets the HTTP upgrade context shared by the connection\.

```csharp
public Microsoft.AspNetCore.Http.HttpContext HttpContext { get; }
```

Implements [HttpContext](DarkWS.Abstractions.IWebSocketConnection.md#DarkWS.Abstractions.IWebSocketConnection.HttpContext 'DarkWS\.Abstractions\.IWebSocketConnection\.HttpContext')

#### Property Value
[Microsoft\.AspNetCore\.Http\.HttpContext](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httpcontext 'Microsoft\.AspNetCore\.Http\.HttpContext')

<a id='DarkWS.WebSocketConnection.Id'></a>

## WebSocketConnection\.Id Property

Gets the stable identity for connection or session targeting\.

```csharp
public string Id { get; }
```

Implements [Id](DarkWS.Abstractions.IWebSocketConnection.md#DarkWS.Abstractions.IWebSocketConnection.Id 'DarkWS\.Abstractions\.IWebSocketConnection\.Id')

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.WebSocketConnection.IsOpen'></a>

## WebSocketConnection\.IsOpen Property

Reports whether the transport is open and has not begun closing\.

```csharp
public bool IsOpen { get; }
```

Implements [IsOpen](DarkWS.Abstractions.IWebSocketConnection.md#DarkWS.Abstractions.IWebSocketConnection.IsOpen 'DarkWS\.Abstractions\.IWebSocketConnection\.IsOpen')

#### Property Value
[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')

<a id='DarkWS.WebSocketConnection.Session'></a>

## WebSocketConnection\.Session Property

Gets the current session, or null for anonymous access\.

```csharp
public DarkWS.Abstractions.IDarkWsSession? Session { get; }
```

Implements [Session](DarkWS.Abstractions.IWebSocketConnection.md#DarkWS.Abstractions.IWebSocketConnection.Session 'DarkWS\.Abstractions\.IWebSocketConnection\.Session')

#### Property Value
[IDarkWsSession](DarkWS.Abstractions.IDarkWsSession.md 'DarkWS\.Abstractions\.IDarkWsSession')

<a id='DarkWS.WebSocketConnection.WebSocket'></a>

## WebSocketConnection\.WebSocket Property

Gets the owned transport\. Use connection methods to preserve write serialization and disposal coordination\.

```csharp
public System.Net.WebSockets.WebSocket WebSocket { get; }
```

Implements [WebSocket](DarkWS.Abstractions.IWebSocketConnection.md#DarkWS.Abstractions.IWebSocketConnection.WebSocket 'DarkWS\.Abstractions\.IWebSocketConnection\.WebSocket')

#### Property Value
[System\.Net\.WebSockets\.WebSocket](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.websocket 'System\.Net\.WebSockets\.WebSocket')
### Methods

<a id='DarkWS.WebSocketConnection.CloseAsync(System.Threading.CancellationToken)'></a>

## WebSocketConnection\.CloseAsync\(CancellationToken\) Method

Stops new writes and performs graceful close\. Supply cancellation to bound the close handshake\.

```csharp
public System.Threading.Tasks.Task CloseAsync(System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.WebSocketConnection.CloseAsync(System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

Implements [CloseAsync\(CancellationToken\)](DarkWS.Abstractions.IWebSocketConnection.md#DarkWS.Abstractions.IWebSocketConnection.CloseAsync(System.Threading.CancellationToken) 'DarkWS\.Abstractions\.IWebSocketConnection\.CloseAsync\(System\.Threading\.CancellationToken\)')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.WebSocketConnection.Dispose()'></a>

## WebSocketConnection\.Dispose\(\) Method

Stops operations and releases transport resources after active I/O finishes\. Repeated calls are safe\.

```csharp
public void Dispose();
```

Implements [Dispose\(\)](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable.dispose 'System\.IDisposable\.Dispose')

<a id='DarkWS.WebSocketConnection.ReceiveMessageAsync(System.Threading.CancellationToken)'></a>

## WebSocketConnection\.ReceiveMessageAsync\(CancellationToken\) Method

Receives a complete message\. Close frames discard partial data; exceeding the size limit closes with status 1009\.

```csharp
public System.Threading.Tasks.Task<DarkWS.ReceivedMessage> ReceiveMessageAsync(System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.WebSocketConnection.ReceiveMessageAsync(System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

Implements [ReceiveMessageAsync\(CancellationToken\)](DarkWS.Abstractions.IWebSocketConnection.md#DarkWS.Abstractions.IWebSocketConnection.ReceiveMessageAsync(System.Threading.CancellationToken) 'DarkWS\.Abstractions\.IWebSocketConnection\.ReceiveMessageAsync\(System\.Threading\.CancellationToken\)')

#### Returns
[System\.Threading\.Tasks\.Task&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1 'System\.Threading\.Tasks\.Task\`1')[ReceivedMessage](DarkWS.ReceivedMessage.md 'DarkWS\.ReceivedMessage')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1 'System\.Threading\.Tasks\.Task\`1')

<a id='DarkWS.WebSocketConnection.SendAsync(byte[],System.Threading.CancellationToken)'></a>

## WebSocketConnection\.SendAsync\(byte\[\], CancellationToken\) Method

Sends immutable bytes with serialized writes\. The built\-in transport bounds lock wait and send time and aborts on timeout; closing connections ignore new writes\.

```csharp
public System.Threading.Tasks.Task SendAsync(byte[] data, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.WebSocketConnection.SendAsync(byte[],System.Threading.CancellationToken).data'></a>

`data` [System\.Byte](https://learn.microsoft.com/en-us/dotnet/api/system.byte 'System\.Byte')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

<a id='DarkWS.WebSocketConnection.SendAsync(byte[],System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

Implements [SendAsync\(byte\[\], CancellationToken\)](DarkWS.Abstractions.IWebSocketConnection.md#DarkWS.Abstractions.IWebSocketConnection.SendAsync(byte[],System.Threading.CancellationToken) 'DarkWS\.Abstractions\.IWebSocketConnection\.SendAsync\(byte\[\], System\.Threading\.CancellationToken\)')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')