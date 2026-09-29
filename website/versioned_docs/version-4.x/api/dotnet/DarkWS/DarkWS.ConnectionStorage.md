#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## ConnectionStorage Class

Thread\-safe local registry with session and group indexes\. Re\-add a connection to refresh externally changed membership\.

```csharp
public sealed class ConnectionStorage
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → ConnectionStorage
### Methods

<a id='DarkWS.ConnectionStorage.Add(DarkWS.Abstractions.IWebSocketConnection)'></a>

## ConnectionStorage\.Add\(IWebSocketConnection\) Method

Adds or replaces a connection by id and refreshes its session/group indexes\.

```csharp
public DarkWS.Abstractions.IWebSocketConnection Add(DarkWS.Abstractions.IWebSocketConnection connection);
```
#### Parameters

<a id='DarkWS.ConnectionStorage.Add(DarkWS.Abstractions.IWebSocketConnection).connection'></a>

`connection` [IWebSocketConnection](DarkWS.Abstractions.IWebSocketConnection.md 'DarkWS\.Abstractions\.IWebSocketConnection')

#### Returns
[IWebSocketConnection](DarkWS.Abstractions.IWebSocketConnection.md 'DarkWS\.Abstractions\.IWebSocketConnection')

<a id='DarkWS.ConnectionStorage.GetAll()'></a>

## ConnectionStorage\.GetAll\(\) Method

Returns a snapshot of every local connection\.

```csharp
public System.Collections.Generic.IReadOnlyCollection<DarkWS.Abstractions.IWebSocketConnection> GetAll();
```

#### Returns
[System\.Collections\.Generic\.IReadOnlyCollection&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')[IWebSocketConnection](DarkWS.Abstractions.IWebSocketConnection.md 'DarkWS\.Abstractions\.IWebSocketConnection')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')

<a id='DarkWS.ConnectionStorage.GetByConnection(string)'></a>

## ConnectionStorage\.GetByConnection\(string\) Method

Returns the matching connection or an empty snapshot\.

```csharp
public System.Collections.Generic.IReadOnlyCollection<DarkWS.Abstractions.IWebSocketConnection> GetByConnection(string connectionId);
```
#### Parameters

<a id='DarkWS.ConnectionStorage.GetByConnection(string).connectionId'></a>

`connectionId` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[System\.Collections\.Generic\.IReadOnlyCollection&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')[IWebSocketConnection](DarkWS.Abstractions.IWebSocketConnection.md 'DarkWS\.Abstractions\.IWebSocketConnection')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')

<a id='DarkWS.ConnectionStorage.GetByGroup(string)'></a>

## ConnectionStorage\.GetByGroup\(string\) Method

Returns indexed group members; work is proportional to the matching connections\.

```csharp
public System.Collections.Generic.IReadOnlyCollection<DarkWS.Abstractions.IWebSocketConnection> GetByGroup(string group);
```
#### Parameters

<a id='DarkWS.ConnectionStorage.GetByGroup(string).group'></a>

`group` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[System\.Collections\.Generic\.IReadOnlyCollection&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')[IWebSocketConnection](DarkWS.Abstractions.IWebSocketConnection.md 'DarkWS\.Abstractions\.IWebSocketConnection')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')

<a id='DarkWS.ConnectionStorage.GetBySession(string)'></a>

## ConnectionStorage\.GetBySession\(string\) Method

Returns indexed session members; work is proportional to the matching connections\.

```csharp
public System.Collections.Generic.IReadOnlyCollection<DarkWS.Abstractions.IWebSocketConnection> GetBySession(string sessionId);
```
#### Parameters

<a id='DarkWS.ConnectionStorage.GetBySession(string).sessionId'></a>

`sessionId` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[System\.Collections\.Generic\.IReadOnlyCollection&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')[IWebSocketConnection](DarkWS.Abstractions.IWebSocketConnection.md 'DarkWS\.Abstractions\.IWebSocketConnection')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')

<a id='DarkWS.ConnectionStorage.Remove(DarkWS.Abstractions.IWebSocketConnection)'></a>

## ConnectionStorage\.Remove\(IWebSocketConnection\) Method

Removes this exact connection and its indexes; returns false when absent or replaced\.

```csharp
public bool Remove(DarkWS.Abstractions.IWebSocketConnection connection);
```
#### Parameters

<a id='DarkWS.ConnectionStorage.Remove(DarkWS.Abstractions.IWebSocketConnection).connection'></a>

`connection` [IWebSocketConnection](DarkWS.Abstractions.IWebSocketConnection.md 'DarkWS\.Abstractions\.IWebSocketConnection')

#### Returns
[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')