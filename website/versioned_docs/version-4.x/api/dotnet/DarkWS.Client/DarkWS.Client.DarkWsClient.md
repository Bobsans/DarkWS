#### [DarkWS\.Client](Overview.md 'Overview')
### [DarkWS\.Client](DarkWS.Client.md 'DarkWS\.Client')

## DarkWsClient Class

A concurrent, lazy DarkWS client\. One instance owns one server session\.

```csharp
public sealed class DarkWsClient : DarkWS.Client.IDarkWsClient, System.IDisposable, System.IAsyncDisposable
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → DarkWsClient

Implements [IDarkWsClient](DarkWS.Client.IDarkWsClient.md 'DarkWS\.Client\.IDarkWsClient'), [System\.IDisposable](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable 'System\.IDisposable'), [System\.IAsyncDisposable](https://learn.microsoft.com/en-us/dotnet/api/system.iasyncdisposable 'System\.IAsyncDisposable')
### Constructors

<a id='DarkWS.Client.DarkWsClient.DarkWsClient(DarkWS.Client.DarkWsClientOptions)'></a>

## DarkWsClient\(DarkWsClientOptions\) Constructor

Validates and snapshots settings\. No connection is opened\.

```csharp
public DarkWsClient(DarkWS.Client.DarkWsClientOptions options);
```
#### Parameters

<a id='DarkWS.Client.DarkWsClient.DarkWsClient(DarkWS.Client.DarkWsClientOptions).options'></a>

`options` [DarkWsClientOptions](DarkWS.Client.DarkWsClientOptions.md 'DarkWS\.Client\.DarkWsClientOptions')

<a id='DarkWS.Client.DarkWsClient.DarkWsClient(System.Uri)'></a>

## DarkWsClient\(Uri\) Constructor

Creates a client using default settings\. No connection is opened\.

```csharp
public DarkWsClient(System.Uri endpoint);
```
#### Parameters

<a id='DarkWS.Client.DarkWsClient.DarkWsClient(System.Uri).endpoint'></a>

`endpoint` [System\.Uri](https://learn.microsoft.com/en-us/dotnet/api/system.uri 'System\.Uri')
### Properties

<a id='DarkWS.Client.DarkWsClient.State'></a>

## DarkWsClient\.State Property

Current connection readiness\.

```csharp
public DarkWS.Client.DarkWsClientState State { get; }
```

Implements [State](DarkWS.Client.IDarkWsClient.md#DarkWS.Client.IDarkWsClient.State 'DarkWS\.Client\.IDarkWsClient\.State')

#### Property Value
[DarkWsClientState](DarkWS.Client.DarkWsClientState.md 'DarkWS\.Client\.DarkWsClientState')
### Methods

<a id='DarkWS.Client.DarkWsClient.AuthenticateAsync(string,System.Threading.CancellationToken)'></a>

## DarkWsClient\.AuthenticateAsync\(string, CancellationToken\) Method

Waits for acknowledged authentication\. The supplied token is not retained for reconnect\.

```csharp
public System.Threading.Tasks.Task AuthenticateAsync(string token, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Client.DarkWsClient.AuthenticateAsync(string,System.Threading.CancellationToken).token'></a>

`token` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsClient.AuthenticateAsync(string,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

Implements [AuthenticateAsync\(string, CancellationToken\)](DarkWS.Client.IDarkWsClient.md#DarkWS.Client.IDarkWsClient.AuthenticateAsync(string,System.Threading.CancellationToken) 'DarkWS\.Client\.IDarkWsClient\.AuthenticateAsync\(string, System\.Threading\.CancellationToken\)')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.Client.DarkWsClient.CloseAsync(System.Threading.CancellationToken)'></a>

## DarkWsClient\.CloseAsync\(CancellationToken\) Method

Stops reconnect and closes the socket\. Explicit ConnectAsync allows reuse\.

```csharp
public System.Threading.Tasks.Task CloseAsync(System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Client.DarkWsClient.CloseAsync(System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

Implements [CloseAsync\(CancellationToken\)](DarkWS.Client.IDarkWsClient.md#DarkWS.Client.IDarkWsClient.CloseAsync(System.Threading.CancellationToken) 'DarkWS\.Client\.IDarkWsClient\.CloseAsync\(System\.Threading\.CancellationToken\)')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.Client.DarkWsClient.ConnectAsync(System.Threading.CancellationToken)'></a>

## DarkWsClient\.ConnectAsync\(CancellationToken\) Method

Starts or joins a connection cycle\. Cancelling the wait does not stop other callers\.

```csharp
public System.Threading.Tasks.Task ConnectAsync(System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Client.DarkWsClient.ConnectAsync(System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

Implements [ConnectAsync\(CancellationToken\)](DarkWS.Client.IDarkWsClient.md#DarkWS.Client.IDarkWsClient.ConnectAsync(System.Threading.CancellationToken) 'DarkWS\.Client\.IDarkWsClient\.ConnectAsync\(System\.Threading\.CancellationToken\)')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.Client.DarkWsClient.Dispose()'></a>

## DarkWsClient\.Dispose\(\) Method

Immediately stops network activity\. Prefer DisposeAsync when awaiting network cleanup is possible\.

```csharp
public void Dispose();
```

Implements [Dispose\(\)](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable.dispose 'System\.IDisposable\.Dispose')

<a id='DarkWS.Client.DarkWsClient.DisposeAsync()'></a>

## DarkWsClient\.DisposeAsync\(\) Method

Stops and awaits internal network tasks\. Does not wait indefinitely for application callbacks\.

```csharp
public System.Threading.Tasks.ValueTask DisposeAsync();
```

Implements [DisposeAsync\(\)](https://learn.microsoft.com/en-us/dotnet/api/system.iasyncdisposable.disposeasync 'System\.IAsyncDisposable\.DisposeAsync')

#### Returns
[System\.Threading\.Tasks\.ValueTask](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask 'System\.Threading\.Tasks\.ValueTask')

<a id='DarkWS.Client.DarkWsClient.LogoutAsync(System.Threading.CancellationToken)'></a>

## DarkWsClient\.LogoutAsync\(CancellationToken\) Method

Disables automatic session authentication and waits for acknowledged logout\.

```csharp
public System.Threading.Tasks.Task LogoutAsync(System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Client.DarkWsClient.LogoutAsync(System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

Implements [LogoutAsync\(CancellationToken\)](DarkWS.Client.IDarkWsClient.md#DarkWS.Client.IDarkWsClient.LogoutAsync(System.Threading.CancellationToken) 'DarkWS\.Client\.IDarkWsClient\.LogoutAsync\(System\.Threading\.CancellationToken\)')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.Client.DarkWsClient.On(string,System.Action)'></a>

## DarkWsClient\.On\(string, Action\) Method

Subscribes to an action, ignoring its optional payload\. Dispose to unsubscribe\.

```csharp
public System.IDisposable On(string action, System.Action handler);
```
#### Parameters

<a id='DarkWS.Client.DarkWsClient.On(string,System.Action).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsClient.On(string,System.Action).handler'></a>

`handler` [System\.Action](https://learn.microsoft.com/en-us/dotnet/api/system.action 'System\.Action')

Implements [On\(string, Action\)](DarkWS.Client.IDarkWsClient.md#DarkWS.Client.IDarkWsClient.On(string,System.Action) 'DarkWS\.Client\.IDarkWsClient\.On\(string, System\.Action\)')

#### Returns
[System\.IDisposable](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable 'System\.IDisposable')

<a id='DarkWS.Client.DarkWsClient.On_T_(string,System.Action_T_)'></a>

## DarkWsClient\.On\<T\>\(string, Action\<T\>\) Method

Subscribes to typed action data\. Callbacks run off the UI context, in delivery order\.

```csharp
public System.IDisposable On<T>(string action, System.Action<T> handler);
```
#### Type parameters

<a id='DarkWS.Client.DarkWsClient.On_T_(string,System.Action_T_).T'></a>

`T`
#### Parameters

<a id='DarkWS.Client.DarkWsClient.On_T_(string,System.Action_T_).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsClient.On_T_(string,System.Action_T_).handler'></a>

`handler` [System\.Action&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')[T](DarkWS.Client.DarkWsClient.md#DarkWS.Client.DarkWsClient.On_T_(string,System.Action_T_).T 'DarkWS\.Client\.DarkWsClient\.On\<T\>\(string, System\.Action\<T\>\)\.T')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')

Implements [On&lt;T&gt;\(string, Action&lt;T&gt;\)](DarkWS.Client.IDarkWsClient.md#DarkWS.Client.IDarkWsClient.On_T_(string,System.Action_T_) 'DarkWS\.Client\.IDarkWsClient\.On\<T\>\(string, System\.Action\<T\>\)')

#### Returns
[System\.IDisposable](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable 'System\.IDisposable')

<a id='DarkWS.Client.DarkWsClient.OnAsync_T_(string,System.Func_T,System.Threading.CancellationToken,System.Threading.Tasks.Task_)'></a>

## DarkWsClient\.OnAsync\<T\>\(string, Func\<T,CancellationToken,Task\>\) Method

Subscribes to async typed callbacks\. Cancellation signals loss of the originating connection\.

```csharp
public System.IDisposable OnAsync<T>(string action, System.Func<T,System.Threading.CancellationToken,System.Threading.Tasks.Task> handler);
```
#### Type parameters

<a id='DarkWS.Client.DarkWsClient.OnAsync_T_(string,System.Func_T,System.Threading.CancellationToken,System.Threading.Tasks.Task_).T'></a>

`T`
#### Parameters

<a id='DarkWS.Client.DarkWsClient.OnAsync_T_(string,System.Func_T,System.Threading.CancellationToken,System.Threading.Tasks.Task_).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsClient.OnAsync_T_(string,System.Func_T,System.Threading.CancellationToken,System.Threading.Tasks.Task_).handler'></a>

`handler` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')[T](DarkWS.Client.DarkWsClient.md#DarkWS.Client.DarkWsClient.OnAsync_T_(string,System.Func_T,System.Threading.CancellationToken,System.Threading.Tasks.Task_).T 'DarkWS\.Client\.DarkWsClient\.OnAsync\<T\>\(string, System\.Func\<T,System\.Threading\.CancellationToken,System\.Threading\.Tasks\.Task\>\)\.T')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')[System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')

Implements [OnAsync&lt;T&gt;\(string, Func&lt;T,CancellationToken,Task&gt;\)](DarkWS.Client.IDarkWsClient.md#DarkWS.Client.IDarkWsClient.OnAsync_T_(string,System.Func_T,System.Threading.CancellationToken,System.Threading.Tasks.Task_) 'DarkWS\.Client\.IDarkWsClient\.OnAsync\<T\>\(string, System\.Func\<T,System\.Threading\.CancellationToken,System\.Threading\.Tasks\.Task\>\)')

#### Returns
[System\.IDisposable](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable 'System\.IDisposable')

<a id='DarkWS.Client.DarkWsClient.RequestAsync(string,object,System.Threading.CancellationToken)'></a>

## DarkWsClient\.RequestAsync\(string, object, CancellationToken\) Method

Sends a payload and waits for success, ignoring returned data\.

```csharp
public System.Threading.Tasks.Task RequestAsync(string action, object? payload, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Client.DarkWsClient.RequestAsync(string,object,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsClient.RequestAsync(string,object,System.Threading.CancellationToken).payload'></a>

`payload` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')

<a id='DarkWS.Client.DarkWsClient.RequestAsync(string,object,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

Implements [RequestAsync\(string, object, CancellationToken\)](DarkWS.Client.IDarkWsClient.md#DarkWS.Client.IDarkWsClient.RequestAsync(string,object,System.Threading.CancellationToken) 'DarkWS\.Client\.IDarkWsClient\.RequestAsync\(string, object, System\.Threading\.CancellationToken\)')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.Client.DarkWsClient.RequestAsync(string,System.Threading.CancellationToken)'></a>

## DarkWsClient\.RequestAsync\(string, CancellationToken\) Method

Sends a request without a payload and waits for success, ignoring returned data\.

```csharp
public System.Threading.Tasks.Task RequestAsync(string action, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Client.DarkWsClient.RequestAsync(string,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsClient.RequestAsync(string,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

Implements [RequestAsync\(string, CancellationToken\)](DarkWS.Client.IDarkWsClient.md#DarkWS.Client.IDarkWsClient.RequestAsync(string,System.Threading.CancellationToken) 'DarkWS\.Client\.IDarkWsClient\.RequestAsync\(string, System\.Threading\.CancellationToken\)')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.Client.DarkWsClient.RequestAsync_TResponse_(string,object,System.Threading.CancellationToken)'></a>

## DarkWsClient\.RequestAsync\<TResponse\>\(string, object, CancellationToken\) Method

Sends a payload, including explicit null, and deserializes the required data field\.

```csharp
public System.Threading.Tasks.Task<TResponse> RequestAsync<TResponse>(string action, object? payload, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Type parameters

<a id='DarkWS.Client.DarkWsClient.RequestAsync_TResponse_(string,object,System.Threading.CancellationToken).TResponse'></a>

`TResponse`
#### Parameters

<a id='DarkWS.Client.DarkWsClient.RequestAsync_TResponse_(string,object,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsClient.RequestAsync_TResponse_(string,object,System.Threading.CancellationToken).payload'></a>

`payload` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')

<a id='DarkWS.Client.DarkWsClient.RequestAsync_TResponse_(string,object,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

Implements [RequestAsync&lt;TResponse&gt;\(string, object, CancellationToken\)](DarkWS.Client.IDarkWsClient.md#DarkWS.Client.IDarkWsClient.RequestAsync_TResponse_(string,object,System.Threading.CancellationToken) 'DarkWS\.Client\.IDarkWsClient\.RequestAsync\<TResponse\>\(string, object, System\.Threading\.CancellationToken\)')

#### Returns
[System\.Threading\.Tasks\.Task&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1 'System\.Threading\.Tasks\.Task\`1')[TResponse](DarkWS.Client.DarkWsClient.md#DarkWS.Client.DarkWsClient.RequestAsync_TResponse_(string,object,System.Threading.CancellationToken).TResponse 'DarkWS\.Client\.DarkWsClient\.RequestAsync\<TResponse\>\(string, object, System\.Threading\.CancellationToken\)\.TResponse')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1 'System\.Threading\.Tasks\.Task\`1')

<a id='DarkWS.Client.DarkWsClient.RequestAsync_TResponse_(string,System.Threading.CancellationToken)'></a>

## DarkWsClient\.RequestAsync\<TResponse\>\(string, CancellationToken\) Method

Sends a request without a payload and deserializes its required data field\.

```csharp
public System.Threading.Tasks.Task<TResponse> RequestAsync<TResponse>(string action, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Type parameters

<a id='DarkWS.Client.DarkWsClient.RequestAsync_TResponse_(string,System.Threading.CancellationToken).TResponse'></a>

`TResponse`
#### Parameters

<a id='DarkWS.Client.DarkWsClient.RequestAsync_TResponse_(string,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsClient.RequestAsync_TResponse_(string,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

Implements [RequestAsync&lt;TResponse&gt;\(string, CancellationToken\)](DarkWS.Client.IDarkWsClient.md#DarkWS.Client.IDarkWsClient.RequestAsync_TResponse_(string,System.Threading.CancellationToken) 'DarkWS\.Client\.IDarkWsClient\.RequestAsync\<TResponse\>\(string, System\.Threading\.CancellationToken\)')

#### Returns
[System\.Threading\.Tasks\.Task&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1 'System\.Threading\.Tasks\.Task\`1')[TResponse](DarkWS.Client.DarkWsClient.md#DarkWS.Client.DarkWsClient.RequestAsync_TResponse_(string,System.Threading.CancellationToken).TResponse 'DarkWS\.Client\.DarkWsClient\.RequestAsync\<TResponse\>\(string, System\.Threading\.CancellationToken\)\.TResponse')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1 'System\.Threading\.Tasks\.Task\`1')
### Events

<a id='DarkWS.Client.DarkWsClient.Error'></a>

## DarkWsClient\.Error Event

Background and subscriber errors\. Request errors are returned through their tasks\.

```csharp
public event EventHandler<DarkWsClientErrorEventArgs>? Error;
```

Implements [Error](DarkWS.Client.IDarkWsClient.md#DarkWS.Client.IDarkWsClient.Error 'DarkWS\.Client\.IDarkWsClient\.Error')

#### Event Type
[System\.EventHandler&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.eventhandler-1 'System\.EventHandler\`1')[DarkWsClientErrorEventArgs](DarkWS.Client.DarkWsClientErrorEventArgs.md 'DarkWS\.Client\.DarkWsClientErrorEventArgs')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.eventhandler-1 'System\.EventHandler\`1')

<a id='DarkWS.Client.DarkWsClient.StateChanged'></a>

## DarkWsClient\.StateChanged Event

Ordered state transitions delivered outside the socket reader and internal locks\.

```csharp
public event EventHandler<DarkWsStateChangedEventArgs>? StateChanged;
```

Implements [StateChanged](DarkWS.Client.IDarkWsClient.md#DarkWS.Client.IDarkWsClient.StateChanged 'DarkWS\.Client\.IDarkWsClient\.StateChanged')

#### Event Type
[System\.EventHandler&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.eventhandler-1 'System\.EventHandler\`1')[DarkWsStateChangedEventArgs](DarkWS.Client.DarkWsStateChangedEventArgs.md 'DarkWS\.Client\.DarkWsStateChangedEventArgs')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.eventhandler-1 'System\.EventHandler\`1')