#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## InputMessage Class

Incoming request envelope\. Id and Action are required; payload nullability follows the action signature\.

```csharp
public sealed record InputMessage : System.IEquatable<DarkWS.InputMessage>
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → InputMessage

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[InputMessage](DarkWS.InputMessage.md 'DarkWS\.InputMessage')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')
### Constructors

<a id='DarkWS.InputMessage.InputMessage(string,string,System.Nullable_System.Text.Json.JsonElement_)'></a>

## InputMessage\(string, string, Nullable\<JsonElement\>\) Constructor

Incoming request envelope\. Id and Action are required; payload nullability follows the action signature\.

```csharp
public InputMessage(string Id, string Action, System.Nullable<System.Text.Json.JsonElement> Payload);
```
#### Parameters

<a id='DarkWS.InputMessage.InputMessage(string,string,System.Nullable_System.Text.Json.JsonElement_).Id'></a>

`Id` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Correlation or identity key\.

<a id='DarkWS.InputMessage.InputMessage(string,string,System.Nullable_System.Text.Json.JsonElement_).Action'></a>

`Action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Application action name\.

<a id='DarkWS.InputMessage.InputMessage(string,string,System.Nullable_System.Text.Json.JsonElement_).Payload'></a>

`Payload` [System\.Nullable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')[System\.Text\.Json\.JsonElement](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement 'System\.Text\.Json\.JsonElement')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')

Optional request data, serialized as the data field\.
### Properties

<a id='DarkWS.InputMessage.Action'></a>

## InputMessage\.Action Property

Application action name\.

```csharp
public string Action { get; init; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.InputMessage.Id'></a>

## InputMessage\.Id Property

Correlation or identity key\.

```csharp
public string Id { get; init; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.InputMessage.Payload'></a>

## InputMessage\.Payload Property

Optional request data, serialized as the data field\.

```csharp
public System.Nullable<System.Text.Json.JsonElement> Payload { get; init; }
```

#### Property Value
[System\.Nullable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')[System\.Text\.Json\.JsonElement](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement 'System\.Text\.Json\.JsonElement')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')