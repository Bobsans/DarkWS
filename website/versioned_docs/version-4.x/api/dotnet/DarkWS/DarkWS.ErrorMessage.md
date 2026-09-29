#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## ErrorMessage Class

Error response carrying a stable code and optional typed details\.

```csharp
public sealed record ErrorMessage : System.IEquatable<DarkWS.ErrorMessage>
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → ErrorMessage

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[ErrorMessage](DarkWS.ErrorMessage.md 'DarkWS\.ErrorMessage')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')
### Constructors

<a id='DarkWS.ErrorMessage.ErrorMessage(string,string)'></a>

## ErrorMessage\(string, string\) Constructor

Error response carrying a stable code and optional typed details\.

```csharp
public ErrorMessage(string Id, string Error);
```
#### Parameters

<a id='DarkWS.ErrorMessage.ErrorMessage(string,string).Id'></a>

`Id` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Correlation or identity key\.

<a id='DarkWS.ErrorMessage.ErrorMessage(string,string).Error'></a>

`Error` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Stable error code\.
### Properties

<a id='DarkWS.ErrorMessage.Error'></a>

## ErrorMessage\.Error Property

Stable error code\.

```csharp
public string Error { get; init; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.ErrorMessage.Id'></a>

## ErrorMessage\.Id Property

Correlation or identity key\.

```csharp
public string Id { get; init; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')