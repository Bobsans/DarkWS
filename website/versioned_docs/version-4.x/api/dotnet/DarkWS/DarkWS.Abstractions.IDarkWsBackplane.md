#### [DarkWS](Overview.md 'Overview')
### [DarkWS\.Abstractions](DarkWS.Abstractions.md 'DarkWS\.Abstractions')

## IDarkWsBackplane Interface

Transport for broadcast delivery across application instances\.

```csharp
public interface IDarkWsBackplane
```
### Methods

<a id='DarkWS.Abstractions.IDarkWsBackplane.PublishAsync(DarkWS.DarkWsBroadcast,System.Threading.CancellationToken)'></a>

## IDarkWsBackplane\.PublishAsync\(DarkWsBroadcast, CancellationToken\) Method

Publishes one targeted broadcast through the backplane\.

```csharp
System.Threading.Tasks.ValueTask PublishAsync(DarkWS.DarkWsBroadcast message, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Abstractions.IDarkWsBackplane.PublishAsync(DarkWS.DarkWsBroadcast,System.Threading.CancellationToken).message'></a>

`message` [DarkWsBroadcast](DarkWS.DarkWsBroadcast.md 'DarkWS\.DarkWsBroadcast')

<a id='DarkWS.Abstractions.IDarkWsBackplane.PublishAsync(DarkWS.DarkWsBroadcast,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.ValueTask](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask 'System\.Threading\.Tasks\.ValueTask')

<a id='DarkWS.Abstractions.IDarkWsBackplane.SubscribeAsync(System.Func_DarkWS.DarkWsBroadcast,System.Threading.CancellationToken,System.Threading.Tasks.ValueTask_,System.Threading.CancellationToken)'></a>

## IDarkWsBackplane\.SubscribeAsync\(Func\<DarkWsBroadcast,CancellationToken,ValueTask\>, CancellationToken\) Method

Installs a local listener receiving messages and their cancellation tokens\.

```csharp
System.Threading.Tasks.ValueTask SubscribeAsync(System.Func<DarkWS.DarkWsBroadcast,System.Threading.CancellationToken,System.Threading.Tasks.ValueTask> listener, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Abstractions.IDarkWsBackplane.SubscribeAsync(System.Func_DarkWS.DarkWsBroadcast,System.Threading.CancellationToken,System.Threading.Tasks.ValueTask_,System.Threading.CancellationToken).listener'></a>

`listener` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')[DarkWsBroadcast](DarkWS.DarkWsBroadcast.md 'DarkWS\.DarkWsBroadcast')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')[System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')[System\.Threading\.Tasks\.ValueTask](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask 'System\.Threading\.Tasks\.ValueTask')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-3 'System\.Func\`3')

<a id='DarkWS.Abstractions.IDarkWsBackplane.SubscribeAsync(System.Func_DarkWS.DarkWsBroadcast,System.Threading.CancellationToken,System.Threading.Tasks.ValueTask_,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.ValueTask](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask 'System\.Threading\.Tasks\.ValueTask')

### Remarks
The Redis implementation rejects duplicate subscriptions\. Its token controls subscription lifetime; unsubscribing cancels active delivery\.

<a id='DarkWS.Abstractions.IDarkWsBackplane.UnsubscribeAsync(System.Threading.CancellationToken)'></a>

## IDarkWsBackplane\.UnsubscribeAsync\(CancellationToken\) Method

Stops local delivery and releases the active subscription\.

```csharp
System.Threading.Tasks.ValueTask UnsubscribeAsync(System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Abstractions.IDarkWsBackplane.UnsubscribeAsync(System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.ValueTask](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask 'System\.Threading\.Tasks\.ValueTask')