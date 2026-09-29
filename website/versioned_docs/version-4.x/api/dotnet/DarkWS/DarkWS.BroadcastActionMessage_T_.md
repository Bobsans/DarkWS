#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## BroadcastActionMessage\<T\> Class

Notification envelope containing the reserved id, application action, and data\.

```csharp
public sealed record BroadcastActionMessage<T> : System.IEquatable<DarkWS.BroadcastActionMessage<T>>
```
#### Type parameters

<a id='DarkWS.BroadcastActionMessage_T_.T'></a>

`T`

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → BroadcastActionMessage\<T\>

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[DarkWS\.BroadcastActionMessage&lt;](DarkWS.BroadcastActionMessage_T_.md 'DarkWS\.BroadcastActionMessage\<T\>')[T](DarkWS.BroadcastActionMessage_T_.md#DarkWS.BroadcastActionMessage_T_.T 'DarkWS\.BroadcastActionMessage\<T\>\.T')[&gt;](DarkWS.BroadcastActionMessage_T_.md 'DarkWS\.BroadcastActionMessage\<T\>')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')
### Constructors

<a id='DarkWS.BroadcastActionMessage_T_.BroadcastActionMessage(string,T)'></a>

## BroadcastActionMessage\(string, T\) Constructor

Notification envelope containing the reserved id, application action, and data\.

```csharp
public BroadcastActionMessage(string Action, T? Data);
```
#### Parameters

<a id='DarkWS.BroadcastActionMessage_T_.BroadcastActionMessage(string,T).Action'></a>

`Action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Application action name\.

<a id='DarkWS.BroadcastActionMessage_T_.BroadcastActionMessage(string,T).Data'></a>

`Data` [T](DarkWS.BroadcastActionMessage_T_.md#DarkWS.BroadcastActionMessage_T_.T 'DarkWS\.BroadcastActionMessage\<T\>\.T')

Optional result or notification data\.
### Properties

<a id='DarkWS.BroadcastActionMessage_T_.Action'></a>

## BroadcastActionMessage\<T\>\.Action Property

Application action name\.

```csharp
public string Action { get; init; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.BroadcastActionMessage_T_.Data'></a>

## BroadcastActionMessage\<T\>\.Data Property

Optional result or notification data\.

```csharp
public T? Data { get; init; }
```

#### Property Value
[T](DarkWS.BroadcastActionMessage_T_.md#DarkWS.BroadcastActionMessage_T_.T 'DarkWS\.BroadcastActionMessage\<T\>\.T')

<a id='DarkWS.BroadcastActionMessage_T_.Id'></a>

## BroadcastActionMessage\<T\>\.Id Property

Reserved notification id\.

```csharp
public string Id { get; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')