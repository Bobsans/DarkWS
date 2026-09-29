#### [DarkWS\.Client](Overview.md 'Overview')
### [DarkWS\.Client](DarkWS.Client.md 'DarkWS\.Client')

## DarkWsResponseException Class

A controlled server error\. Payloads are excluded from the exception message\.

```csharp
public sealed class DarkWsResponseException : System.Exception
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') → DarkWsResponseException
### Constructors

<a id='DarkWS.Client.DarkWsResponseException.DarkWsResponseException(string,System.Nullable_System.Text.Json.JsonElement_,string,string)'></a>

## DarkWsResponseException\(string, Nullable\<JsonElement\>, string, string\) Constructor

Creates a correlated server error with independently owned JSON data\.

```csharp
public DarkWsResponseException(string code, System.Nullable<System.Text.Json.JsonElement> errorData, string requestId, string action);
```
#### Parameters

<a id='DarkWS.Client.DarkWsResponseException.DarkWsResponseException(string,System.Nullable_System.Text.Json.JsonElement_,string,string).code'></a>

`code` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsResponseException.DarkWsResponseException(string,System.Nullable_System.Text.Json.JsonElement_,string,string).errorData'></a>

`errorData` [System\.Nullable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')[System\.Text\.Json\.JsonElement](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement 'System\.Text\.Json\.JsonElement')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')

<a id='DarkWS.Client.DarkWsResponseException.DarkWsResponseException(string,System.Nullable_System.Text.Json.JsonElement_,string,string).requestId'></a>

`requestId` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsResponseException.DarkWsResponseException(string,System.Nullable_System.Text.Json.JsonElement_,string,string).action'></a>

`action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')
### Properties

<a id='DarkWS.Client.DarkWsResponseException.Action'></a>

## DarkWsResponseException\.Action Property

Requested action\.

```csharp
public string Action { get; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsResponseException.Code'></a>

## DarkWsResponseException\.Code Property

Server error code, possibly empty\.

```csharp
public string Code { get; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Client.DarkWsResponseException.ErrorData'></a>

## DarkWsResponseException\.ErrorData Property

Optional server error data, independent of receive buffers\.

```csharp
public System.Nullable<System.Text.Json.JsonElement> ErrorData { get; }
```

#### Property Value
[System\.Nullable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')[System\.Text\.Json\.JsonElement](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement 'System\.Text\.Json\.JsonElement')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')

<a id='DarkWS.Client.DarkWsResponseException.RequestId'></a>

## DarkWsResponseException\.RequestId Property

Correlated request identifier, or empty for a text system command\.

```csharp
public string RequestId { get; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')