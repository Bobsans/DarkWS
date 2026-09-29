#### [DarkWS\.Client](Overview.md 'Overview')
### [DarkWS\.Client](DarkWS.Client.md 'DarkWS\.Client')

## DarkWsProtocolException Class

A malformed or unsupported incoming message\.

```csharp
public sealed class DarkWsProtocolException : System.Exception
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') → DarkWsProtocolException
### Constructors

<a id='DarkWS.Client.DarkWsProtocolException.DarkWsProtocolException(string,System.Net.WebSockets.WebSocketCloseStatus)'></a>

## DarkWsProtocolException\(string, WebSocketCloseStatus\) Constructor

Creates a protocol error with the close status used for fatal wire violations\.

```csharp
public DarkWsProtocolException(string message, System.Net.WebSockets.WebSocketCloseStatus closeStatus=System.Net.WebSockets.WebSocketCloseStatus.ProtocolError);
```
#### Parameters

<a id='DarkWS.Client.DarkWsProtocolException.DarkWsProtocolException(string,System.Net.WebSockets.WebSocketCloseStatus).message'></a>

`message` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsProtocolException.DarkWsProtocolException(string,System.Net.WebSockets.WebSocketCloseStatus).closeStatus'></a>

`closeStatus` [System\.Net\.WebSockets\.WebSocketCloseStatus](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.websocketclosestatus 'System\.Net\.WebSockets\.WebSocketCloseStatus')
### Properties

<a id='DarkWS.Client.DarkWsProtocolException.CloseStatus'></a>

## DarkWsProtocolException\.CloseStatus Property

Close status used when the violation affects the connection\.

```csharp
public System.Net.WebSockets.WebSocketCloseStatus CloseStatus { get; }
```

#### Property Value
[System\.Net\.WebSockets\.WebSocketCloseStatus](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.websocketclosestatus 'System\.Net\.WebSockets\.WebSocketCloseStatus')