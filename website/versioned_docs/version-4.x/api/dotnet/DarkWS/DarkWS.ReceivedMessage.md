#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## ReceivedMessage Class

Complete received message, or a close result with an empty data buffer\.

```csharp
public class ReceivedMessage : System.Net.WebSockets.WebSocketReceiveResult
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Net\.WebSockets\.WebSocketReceiveResult](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.websocketreceiveresult 'System\.Net\.WebSockets\.WebSocketReceiveResult') → ReceivedMessage
### Constructors

<a id='DarkWS.ReceivedMessage.ReceivedMessage(System.Net.WebSockets.WebSocketReceiveResult,byte[])'></a>

## ReceivedMessage\(WebSocketReceiveResult, byte\[\]\) Constructor

Complete received message, or a close result with an empty data buffer\.

```csharp
public ReceivedMessage(System.Net.WebSockets.WebSocketReceiveResult result, byte[] data);
```
#### Parameters

<a id='DarkWS.ReceivedMessage.ReceivedMessage(System.Net.WebSockets.WebSocketReceiveResult,byte[]).result'></a>

`result` [System\.Net\.WebSockets\.WebSocketReceiveResult](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.websocketreceiveresult 'System\.Net\.WebSockets\.WebSocketReceiveResult')

Transport receive metadata\.

<a id='DarkWS.ReceivedMessage.ReceivedMessage(System.Net.WebSockets.WebSocketReceiveResult,byte[]).data'></a>

`data` [System\.Byte](https://learn.microsoft.com/en-us/dotnet/api/system.byte 'System\.Byte')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

Result or message data\.
### Properties

<a id='DarkWS.ReceivedMessage.Data'></a>

## ReceivedMessage\.Data Property

Gets complete message bytes; empty for close results\.

```csharp
public byte[] Data { get; }
```

#### Property Value
[System\.Byte](https://learn.microsoft.com/en-us/dotnet/api/system.byte 'System\.Byte')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')