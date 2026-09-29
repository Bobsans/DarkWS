#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## OkMessage Class

Successful response without data, correlated by request id\.

```csharp
public sealed record OkMessage : System.IEquatable<DarkWS.OkMessage>
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → OkMessage

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[OkMessage](DarkWS.OkMessage.md 'DarkWS\.OkMessage')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')
### Constructors

<a id='DarkWS.OkMessage.OkMessage(string)'></a>

## OkMessage\(string\) Constructor

Successful response without data, correlated by request id\.

```csharp
public OkMessage(string Id);
```
#### Parameters

<a id='DarkWS.OkMessage.OkMessage(string).Id'></a>

`Id` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Correlation or identity key\.
### Properties

<a id='DarkWS.OkMessage.Id'></a>

## OkMessage\.Id Property

Correlation or identity key\.

```csharp
public string Id { get; init; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')