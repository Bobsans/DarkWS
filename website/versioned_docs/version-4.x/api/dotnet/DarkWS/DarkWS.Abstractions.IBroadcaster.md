#### [DarkWS](Overview.md 'Overview')
### [DarkWS\.Abstractions](DarkWS.Abstractions.md 'DarkWS\.Abstractions')

## IBroadcaster Interface

Publishes notifications through the configured backplane\. Recipients share an immutable envelope with id @\.

```csharp
public interface IBroadcaster
```
### Methods

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastAsync(string,System.Threading.CancellationToken)'></a>

## IBroadcaster\.BroadcastAsync\(string, CancellationToken\) Method

Publishes an action and optional data to all connections through the backplane\.

```csharp
System.Threading.Tasks.Task BroadcastAsync(string action, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastAsync(string,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastAsync(string,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastAsync_T_(string,T,System.Threading.CancellationToken)'></a>

## IBroadcaster\.BroadcastAsync\<T\>\(string, T, CancellationToken\) Method

Publishes an action and optional data to all connections through the backplane\.

```csharp
System.Threading.Tasks.Task BroadcastAsync<T>(string action, T? data, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Type parameters

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastAsync_T_(string,T,System.Threading.CancellationToken).T'></a>

`T`
#### Parameters

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastAsync_T_(string,T,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastAsync_T_(string,T,System.Threading.CancellationToken).data'></a>

`data` [T](DarkWS.Abstractions.IBroadcaster.md#DarkWS.Abstractions.IBroadcaster.BroadcastAsync_T_(string,T,System.Threading.CancellationToken).T 'DarkWS\.Abstractions\.IBroadcaster\.BroadcastAsync\<T\>\(string, T, System\.Threading\.CancellationToken\)\.T')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastAsync_T_(string,T,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToConnectionAsync(string,string,System.Threading.CancellationToken)'></a>

## IBroadcaster\.BroadcastToConnectionAsync\(string, string, CancellationToken\) Method

Publishes an action and optional data to the specified connection id\.

```csharp
System.Threading.Tasks.Task BroadcastToConnectionAsync(string connectionId, string action, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToConnectionAsync(string,string,System.Threading.CancellationToken).connectionId'></a>

`connectionId` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToConnectionAsync(string,string,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToConnectionAsync(string,string,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToConnectionAsync_T_(string,string,T,System.Threading.CancellationToken)'></a>

## IBroadcaster\.BroadcastToConnectionAsync\<T\>\(string, string, T, CancellationToken\) Method

Publishes an action and optional data to the specified connection id\.

```csharp
System.Threading.Tasks.Task BroadcastToConnectionAsync<T>(string connectionId, string action, T? data, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Type parameters

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToConnectionAsync_T_(string,string,T,System.Threading.CancellationToken).T'></a>

`T`
#### Parameters

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToConnectionAsync_T_(string,string,T,System.Threading.CancellationToken).connectionId'></a>

`connectionId` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToConnectionAsync_T_(string,string,T,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToConnectionAsync_T_(string,string,T,System.Threading.CancellationToken).data'></a>

`data` [T](DarkWS.Abstractions.IBroadcaster.md#DarkWS.Abstractions.IBroadcaster.BroadcastToConnectionAsync_T_(string,string,T,System.Threading.CancellationToken).T 'DarkWS\.Abstractions\.IBroadcaster\.BroadcastToConnectionAsync\<T\>\(string, string, T, System\.Threading\.CancellationToken\)\.T')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToConnectionAsync_T_(string,string,T,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToGroupAsync(string,string,System.Threading.CancellationToken)'></a>

## IBroadcaster\.BroadcastToGroupAsync\(string, string, CancellationToken\) Method

Publishes an action and optional data to members of the specified group\.

```csharp
System.Threading.Tasks.Task BroadcastToGroupAsync(string group, string action, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToGroupAsync(string,string,System.Threading.CancellationToken).group'></a>

`group` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToGroupAsync(string,string,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToGroupAsync(string,string,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToGroupAsync_T_(string,string,T,System.Threading.CancellationToken)'></a>

## IBroadcaster\.BroadcastToGroupAsync\<T\>\(string, string, T, CancellationToken\) Method

Publishes an action and optional data to members of the specified group\.

```csharp
System.Threading.Tasks.Task BroadcastToGroupAsync<T>(string group, string action, T? data, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Type parameters

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToGroupAsync_T_(string,string,T,System.Threading.CancellationToken).T'></a>

`T`
#### Parameters

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToGroupAsync_T_(string,string,T,System.Threading.CancellationToken).group'></a>

`group` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToGroupAsync_T_(string,string,T,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToGroupAsync_T_(string,string,T,System.Threading.CancellationToken).data'></a>

`data` [T](DarkWS.Abstractions.IBroadcaster.md#DarkWS.Abstractions.IBroadcaster.BroadcastToGroupAsync_T_(string,string,T,System.Threading.CancellationToken).T 'DarkWS\.Abstractions\.IBroadcaster\.BroadcastToGroupAsync\<T\>\(string, string, T, System\.Threading\.CancellationToken\)\.T')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToGroupAsync_T_(string,string,T,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToSessionAsync(string,string,System.Threading.CancellationToken)'></a>

## IBroadcaster\.BroadcastToSessionAsync\(string, string, CancellationToken\) Method

Publishes an action and optional data to connections in the specified session\.

```csharp
System.Threading.Tasks.Task BroadcastToSessionAsync(string sessionId, string action, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToSessionAsync(string,string,System.Threading.CancellationToken).sessionId'></a>

`sessionId` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToSessionAsync(string,string,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToSessionAsync(string,string,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToSessionAsync_T_(string,string,T,System.Threading.CancellationToken)'></a>

## IBroadcaster\.BroadcastToSessionAsync\<T\>\(string, string, T, CancellationToken\) Method

Publishes an action and optional data to connections in the specified session\.

```csharp
System.Threading.Tasks.Task BroadcastToSessionAsync<T>(string sessionId, string action, T? data, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Type parameters

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToSessionAsync_T_(string,string,T,System.Threading.CancellationToken).T'></a>

`T`
#### Parameters

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToSessionAsync_T_(string,string,T,System.Threading.CancellationToken).sessionId'></a>

`sessionId` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToSessionAsync_T_(string,string,T,System.Threading.CancellationToken).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToSessionAsync_T_(string,string,T,System.Threading.CancellationToken).data'></a>

`data` [T](DarkWS.Abstractions.IBroadcaster.md#DarkWS.Abstractions.IBroadcaster.BroadcastToSessionAsync_T_(string,string,T,System.Threading.CancellationToken).T 'DarkWS\.Abstractions\.IBroadcaster\.BroadcastToSessionAsync\<T\>\(string, string, T, System\.Threading\.CancellationToken\)\.T')

<a id='DarkWS.Abstractions.IBroadcaster.BroadcastToSessionAsync_T_(string,string,T,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')