#### [DarkWS\.Client](Overview.md 'Overview')
### [DarkWS\.Client](DarkWS.Client.md 'DarkWS\.Client')

## DarkWsConnectionException Class

A socket or connection lifecycle failure\.

```csharp
public sealed class DarkWsConnectionException : System.Exception
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') → DarkWsConnectionException
### Constructors

<a id='DarkWS.Client.DarkWsConnectionException.DarkWsConnectionException(string,System.Nullable_System.Net.WebSockets.WebSocketCloseStatus_,string,System.Exception)'></a>

## DarkWsConnectionException\(string, Nullable\<WebSocketCloseStatus\>, string, Exception\) Constructor

Creates a connection failure\. Avoid sensitive content in the supplied message or cause\.

```csharp
public DarkWsConnectionException(string message, System.Nullable<System.Net.WebSockets.WebSocketCloseStatus> closeStatus=null, string? closeReason=null, System.Exception? innerException=null);
```
#### Parameters

<a id='DarkWS.Client.DarkWsConnectionException.DarkWsConnectionException(string,System.Nullable_System.Net.WebSockets.WebSocketCloseStatus_,string,System.Exception).message'></a>

`message` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsConnectionException.DarkWsConnectionException(string,System.Nullable_System.Net.WebSockets.WebSocketCloseStatus_,string,System.Exception).closeStatus'></a>

`closeStatus` [System\.Nullable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')[System\.Net\.WebSockets\.WebSocketCloseStatus](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.websocketclosestatus 'System\.Net\.WebSockets\.WebSocketCloseStatus')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')

<a id='DarkWS.Client.DarkWsConnectionException.DarkWsConnectionException(string,System.Nullable_System.Net.WebSockets.WebSocketCloseStatus_,string,System.Exception).closeReason'></a>

`closeReason` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsConnectionException.DarkWsConnectionException(string,System.Nullable_System.Net.WebSockets.WebSocketCloseStatus_,string,System.Exception).innerException'></a>

`innerException` [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception')
### Properties

<a id='DarkWS.Client.DarkWsConnectionException.CloseReason'></a>

## DarkWsConnectionException\.CloseReason Property

Peer\-provided close reason\. May contain application data; excluded from Message\.

```csharp
public string? CloseReason { get; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsConnectionException.CloseStatus'></a>

## DarkWsConnectionException\.CloseStatus Property

Peer close status, when available\.

```csharp
public System.Nullable<System.Net.WebSockets.WebSocketCloseStatus> CloseStatus { get; }
```

#### Property Value
[System\.Nullable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')[System\.Net\.WebSockets\.WebSocketCloseStatus](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.websocketclosestatus 'System\.Net\.WebSockets\.WebSocketCloseStatus')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')