#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## BroadcastActionMessage Class

Notification envelope containing the reserved id and application action\.

```csharp
public sealed record BroadcastActionMessage : System.IEquatable<DarkWS.BroadcastActionMessage>
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → BroadcastActionMessage

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[BroadcastActionMessage](DarkWS.BroadcastActionMessage.md 'DarkWS\.BroadcastActionMessage')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')
### Constructors

<a id='DarkWS.BroadcastActionMessage.BroadcastActionMessage(string)'></a>

## BroadcastActionMessage\(string\) Constructor

Notification envelope containing the reserved id and application action\.

```csharp
public BroadcastActionMessage(string Action);
```
#### Parameters

<a id='DarkWS.BroadcastActionMessage.BroadcastActionMessage(string).Action'></a>

`Action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Application action name\.
### Properties

<a id='DarkWS.BroadcastActionMessage.Action'></a>

## BroadcastActionMessage\.Action Property

Application action name\.

```csharp
public string Action { get; init; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.BroadcastActionMessage.Id'></a>

## BroadcastActionMessage\.Id Property

Reserved notification id\.

```csharp
public string Id { get; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')