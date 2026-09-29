#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## HandlerBase Class

Base for per\-message handlers\. Context\-dependent members are available only during action invocation\.

```csharp
public abstract class HandlerBase
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → HandlerBase

Derived  
↳ [HandlerBase&lt;TSession&gt;](DarkWS.HandlerBase_TSession_.md 'DarkWS\.HandlerBase\<TSession\>')
### Properties

<a id='DarkWS.HandlerBase.AspNetSession'></a>

## HandlerBase\.AspNetSession Property

Gets ASP\.NET session state when its middleware is installed, otherwise null\.

```csharp
protected Microsoft.AspNetCore.Http.ISession? AspNetSession { protected get; }
```

#### Property Value
[Microsoft\.AspNetCore\.Http\.ISession](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.isession 'Microsoft\.AspNetCore\.Http\.ISession')

<a id='DarkWS.HandlerBase.Connection'></a>

## HandlerBase\.Connection Property

Gets the current connection during initialized message handling\.

```csharp
protected DarkWS.Abstractions.IWebSocketConnection Connection { protected get; }
```

#### Property Value
[IWebSocketConnection](DarkWS.Abstractions.IWebSocketConnection.md 'DarkWS\.Abstractions\.IWebSocketConnection')

<a id='DarkWS.HandlerBase.ConnectionAborted'></a>

## HandlerBase\.ConnectionAborted Property

Gets the token signaled when the connection stops\. Handlers should observe it during asynchronous work\.

```csharp
protected System.Threading.CancellationToken ConnectionAborted { protected get; }
```

#### Property Value
[System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

<a id='DarkWS.HandlerBase.HttpContext'></a>

## HandlerBase\.HttpContext Property

Gets the HTTP upgrade context shared by the connection\.

```csharp
protected Microsoft.AspNetCore.Http.HttpContext HttpContext { protected get; }
```

#### Property Value
[Microsoft\.AspNetCore\.Http\.HttpContext](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httpcontext 'Microsoft\.AspNetCore\.Http\.HttpContext')

<a id='DarkWS.HandlerBase.Session'></a>

## HandlerBase\.Session Property

Gets the required handler session\. Throws InvalidOperationException for an anonymous connection or incompatible typed session\.

```csharp
protected DarkWS.Abstractions.IDarkWsSession Session { protected get; }
```

#### Property Value
[IDarkWsSession](DarkWS.Abstractions.IDarkWsSession.md 'DarkWS\.Abstractions\.IDarkWsSession')
### Methods

<a id='DarkWS.HandlerBase.BroadcastAsync(string,System.Threading.CancellationToken)'></a>

## HandlerBase\.BroadcastAsync\(string, CancellationToken\) Method

Publishes an action and optional data to all connections through the backplane\.

```csharp
protected System.Threading.Tasks.Task BroadcastAsync(string action, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.HandlerBase.BroadcastAsync(string,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.HandlerBase.BroadcastAsync(string,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.HandlerBase.BroadcastAsync_T_(string,T,System.Threading.CancellationToken)'></a>

## HandlerBase\.BroadcastAsync\<T\>\(string, T, CancellationToken\) Method

Publishes an action and optional data to all connections through the backplane\.

```csharp
protected System.Threading.Tasks.Task BroadcastAsync<T>(string action, T? data, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Type parameters

<a id='DarkWS.HandlerBase.BroadcastAsync_T_(string,T,System.Threading.CancellationToken).T'></a>

`T`
#### Parameters

<a id='DarkWS.HandlerBase.BroadcastAsync_T_(string,T,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.HandlerBase.BroadcastAsync_T_(string,T,System.Threading.CancellationToken).data'></a>

`data` [T](DarkWS.HandlerBase.md#DarkWS.HandlerBase.BroadcastAsync_T_(string,T,System.Threading.CancellationToken).T 'DarkWS\.HandlerBase\.BroadcastAsync\<T\>\(string, T, System\.Threading\.CancellationToken\)\.T')

<a id='DarkWS.HandlerBase.BroadcastAsync_T_(string,T,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.HandlerBase.BroadcastToGroupAsync(string,string,System.Threading.CancellationToken)'></a>

## HandlerBase\.BroadcastToGroupAsync\(string, string, CancellationToken\) Method

Publishes an action and optional data to members of the specified group\.

```csharp
protected System.Threading.Tasks.Task BroadcastToGroupAsync(string group, string action, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.HandlerBase.BroadcastToGroupAsync(string,string,System.Threading.CancellationToken).group'></a>

`group` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.HandlerBase.BroadcastToGroupAsync(string,string,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.HandlerBase.BroadcastToGroupAsync(string,string,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.HandlerBase.BroadcastToGroupAsync_T_(string,string,T,System.Threading.CancellationToken)'></a>

## HandlerBase\.BroadcastToGroupAsync\<T\>\(string, string, T, CancellationToken\) Method

Publishes an action and optional data to members of the specified group\.

```csharp
protected System.Threading.Tasks.Task BroadcastToGroupAsync<T>(string group, string action, T? data, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Type parameters

<a id='DarkWS.HandlerBase.BroadcastToGroupAsync_T_(string,string,T,System.Threading.CancellationToken).T'></a>

`T`
#### Parameters

<a id='DarkWS.HandlerBase.BroadcastToGroupAsync_T_(string,string,T,System.Threading.CancellationToken).group'></a>

`group` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.HandlerBase.BroadcastToGroupAsync_T_(string,string,T,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.HandlerBase.BroadcastToGroupAsync_T_(string,string,T,System.Threading.CancellationToken).data'></a>

`data` [T](DarkWS.HandlerBase.md#DarkWS.HandlerBase.BroadcastToGroupAsync_T_(string,string,T,System.Threading.CancellationToken).T 'DarkWS\.HandlerBase\.BroadcastToGroupAsync\<T\>\(string, string, T, System\.Threading\.CancellationToken\)\.T')

<a id='DarkWS.HandlerBase.BroadcastToGroupAsync_T_(string,string,T,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.HandlerBase.BroadcastToSelfAsync(string,System.Threading.CancellationToken)'></a>

## HandlerBase\.BroadcastToSelfAsync\(string, CancellationToken\) Method

Publishes an action and optional data to the current handler connection\.

```csharp
protected System.Threading.Tasks.Task BroadcastToSelfAsync(string action, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.HandlerBase.BroadcastToSelfAsync(string,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.HandlerBase.BroadcastToSelfAsync(string,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.HandlerBase.BroadcastToSelfAsync_T_(string,T,System.Threading.CancellationToken)'></a>

## HandlerBase\.BroadcastToSelfAsync\<T\>\(string, T, CancellationToken\) Method

Publishes an action and optional data to the current handler connection\.

```csharp
protected System.Threading.Tasks.Task BroadcastToSelfAsync<T>(string action, T? data, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Type parameters

<a id='DarkWS.HandlerBase.BroadcastToSelfAsync_T_(string,T,System.Threading.CancellationToken).T'></a>

`T`
#### Parameters

<a id='DarkWS.HandlerBase.BroadcastToSelfAsync_T_(string,T,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.HandlerBase.BroadcastToSelfAsync_T_(string,T,System.Threading.CancellationToken).data'></a>

`data` [T](DarkWS.HandlerBase.md#DarkWS.HandlerBase.BroadcastToSelfAsync_T_(string,T,System.Threading.CancellationToken).T 'DarkWS\.HandlerBase\.BroadcastToSelfAsync\<T\>\(string, T, System\.Threading\.CancellationToken\)\.T')

<a id='DarkWS.HandlerBase.BroadcastToSelfAsync_T_(string,T,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.HandlerBase.BroadcastToSessionAsync(string,string,System.Threading.CancellationToken)'></a>

## HandlerBase\.BroadcastToSessionAsync\(string, string, CancellationToken\) Method

Publishes an action and optional data to connections in the specified session\.

```csharp
protected System.Threading.Tasks.Task BroadcastToSessionAsync(string sessionId, string action, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.HandlerBase.BroadcastToSessionAsync(string,string,System.Threading.CancellationToken).sessionId'></a>

`sessionId` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.HandlerBase.BroadcastToSessionAsync(string,string,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.HandlerBase.BroadcastToSessionAsync(string,string,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.HandlerBase.BroadcastToSessionAsync_T_(string,string,T,System.Threading.CancellationToken)'></a>

## HandlerBase\.BroadcastToSessionAsync\<T\>\(string, string, T, CancellationToken\) Method

Publishes an action and optional data to connections in the specified session\.

```csharp
protected System.Threading.Tasks.Task BroadcastToSessionAsync<T>(string sessionId, string action, T? data, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Type parameters

<a id='DarkWS.HandlerBase.BroadcastToSessionAsync_T_(string,string,T,System.Threading.CancellationToken).T'></a>

`T`
#### Parameters

<a id='DarkWS.HandlerBase.BroadcastToSessionAsync_T_(string,string,T,System.Threading.CancellationToken).sessionId'></a>

`sessionId` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.HandlerBase.BroadcastToSessionAsync_T_(string,string,T,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.HandlerBase.BroadcastToSessionAsync_T_(string,string,T,System.Threading.CancellationToken).data'></a>

`data` [T](DarkWS.HandlerBase.md#DarkWS.HandlerBase.BroadcastToSessionAsync_T_(string,string,T,System.Threading.CancellationToken).T 'DarkWS\.HandlerBase\.BroadcastToSessionAsync\<T\>\(string, string, T, System\.Threading\.CancellationToken\)\.T')

<a id='DarkWS.HandlerBase.BroadcastToSessionAsync_T_(string,string,T,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.HandlerBase.Error(string)'></a>

## HandlerBase\.Error\(string\) Method

Creates an error action result with a stable code and optional details\.

```csharp
protected static DarkWS.IResponse Error(string error);
```
#### Parameters

<a id='DarkWS.HandlerBase.Error(string).error'></a>

`error` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[IResponse](DarkWS.IResponse.md 'DarkWS\.IResponse')

<a id='DarkWS.HandlerBase.Error_T_(string,T)'></a>

## HandlerBase\.Error\<T\>\(string, T\) Method

Creates an error action result with a stable code and optional details\.

```csharp
protected static DarkWS.IResponse Error<T>(string error, T details);
```
#### Type parameters

<a id='DarkWS.HandlerBase.Error_T_(string,T).T'></a>

`T`
#### Parameters

<a id='DarkWS.HandlerBase.Error_T_(string,T).error'></a>

`error` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.HandlerBase.Error_T_(string,T).details'></a>

`details` [T](DarkWS.HandlerBase.md#DarkWS.HandlerBase.Error_T_(string,T).T 'DarkWS\.HandlerBase\.Error\<T\>\(string, T\)\.T')

#### Returns
[IResponse](DarkWS.IResponse.md 'DarkWS\.IResponse')

<a id='DarkWS.HandlerBase.Ok()'></a>

## HandlerBase\.Ok\(\) Method

Creates a successful action result with optional typed data\.

```csharp
protected static DarkWS.IResponse Ok();
```

#### Returns
[IResponse](DarkWS.IResponse.md 'DarkWS\.IResponse')

<a id='DarkWS.HandlerBase.Ok_T_(T)'></a>

## HandlerBase\.Ok\<T\>\(T\) Method

Creates a successful action result with optional typed data\.

```csharp
protected static DarkWS.IResponse Ok<T>(T data);
```
#### Type parameters

<a id='DarkWS.HandlerBase.Ok_T_(T).T'></a>

`T`
#### Parameters

<a id='DarkWS.HandlerBase.Ok_T_(T).data'></a>

`data` [T](DarkWS.HandlerBase.md#DarkWS.HandlerBase.Ok_T_(T).T 'DarkWS\.HandlerBase\.Ok\<T\>\(T\)\.T')

#### Returns
[IResponse](DarkWS.IResponse.md 'DarkWS\.IResponse')