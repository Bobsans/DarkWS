#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## ResponseContext Class

Serializes an action response and sends it to the requesting connection\.

```csharp
public sealed class ResponseContext
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → ResponseContext
### Constructors

<a id='DarkWS.ResponseContext.ResponseContext(DarkWS.Abstractions.IWebSocketConnection,string,DarkWS.DarkWsOptions)'></a>

## ResponseContext\(IWebSocketConnection, string, DarkWsOptions\) Constructor

Serializes an action response and sends it to the requesting connection\.

```csharp
public ResponseContext(DarkWS.Abstractions.IWebSocketConnection connection, string requestId, DarkWS.DarkWsOptions options);
```
#### Parameters

<a id='DarkWS.ResponseContext.ResponseContext(DarkWS.Abstractions.IWebSocketConnection,string,DarkWS.DarkWsOptions).connection'></a>

`connection` [IWebSocketConnection](DarkWS.Abstractions.IWebSocketConnection.md 'DarkWS\.Abstractions\.IWebSocketConnection')

Connection receiving the response\.

<a id='DarkWS.ResponseContext.ResponseContext(DarkWS.Abstractions.IWebSocketConnection,string,DarkWS.DarkWsOptions).requestId'></a>

`requestId` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Correlation id from the request\.

<a id='DarkWS.ResponseContext.ResponseContext(DarkWS.Abstractions.IWebSocketConnection,string,DarkWS.DarkWsOptions).options'></a>

`options` [DarkWsOptions](DarkWS.DarkWsOptions.md 'DarkWS\.DarkWsOptions')

Validated serialization and transport settings\.
### Properties

<a id='DarkWS.ResponseContext.RequestId'></a>

## ResponseContext\.RequestId Property

Gets the id used to correlate the response with its request\.

```csharp
public string RequestId { get; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')
### Methods

<a id='DarkWS.ResponseContext.SendAsync_T_(T,System.Threading.CancellationToken)'></a>

## ResponseContext\.SendAsync\<T\>\(T, CancellationToken\) Method

Serializes data using configured JSON options and sends it to the requesting connection\.

```csharp
public System.Threading.Tasks.Task SendAsync<T>(T data, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Type parameters

<a id='DarkWS.ResponseContext.SendAsync_T_(T,System.Threading.CancellationToken).T'></a>

`T`
#### Parameters

<a id='DarkWS.ResponseContext.SendAsync_T_(T,System.Threading.CancellationToken).data'></a>

`data` [T](DarkWS.ResponseContext.md#DarkWS.ResponseContext.SendAsync_T_(T,System.Threading.CancellationToken).T 'DarkWS\.ResponseContext\.SendAsync\<T\>\(T, System\.Threading\.CancellationToken\)\.T')

<a id='DarkWS.ResponseContext.SendAsync_T_(T,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')