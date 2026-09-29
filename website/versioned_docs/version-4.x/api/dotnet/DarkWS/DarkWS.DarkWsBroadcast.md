#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## DarkWsBroadcast Class

Backplane message carrying the recipient selector, action name, and optional JSON data\.

```csharp
public sealed record DarkWsBroadcast : System.IEquatable<DarkWS.DarkWsBroadcast>
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → DarkWsBroadcast

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[DarkWsBroadcast](DarkWS.DarkWsBroadcast.md 'DarkWS\.DarkWsBroadcast')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')
### Constructors

<a id='DarkWS.DarkWsBroadcast.DarkWsBroadcast(DarkWS.DarkWsTarget,string,string,System.Nullable_System.Text.Json.JsonElement_)'></a>

## DarkWsBroadcast\(DarkWsTarget, string, string, Nullable\<JsonElement\>\) Constructor

Backplane message carrying the recipient selector, action name, and optional JSON data\.

```csharp
public DarkWsBroadcast(DarkWS.DarkWsTarget Target, string? TargetId, string Action, System.Nullable<System.Text.Json.JsonElement> Data);
```
#### Parameters

<a id='DarkWS.DarkWsBroadcast.DarkWsBroadcast(DarkWS.DarkWsTarget,string,string,System.Nullable_System.Text.Json.JsonElement_).Target'></a>

`Target` [DarkWsTarget](DarkWS.DarkWsTarget.md 'DarkWS\.DarkWsTarget')

Recipient selector\.

<a id='DarkWS.DarkWsBroadcast.DarkWsBroadcast(DarkWS.DarkWsTarget,string,string,System.Nullable_System.Text.Json.JsonElement_).TargetId'></a>

`TargetId` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Connection, session, or group key; null for all recipients\.

<a id='DarkWS.DarkWsBroadcast.DarkWsBroadcast(DarkWS.DarkWsTarget,string,string,System.Nullable_System.Text.Json.JsonElement_).Action'></a>

`Action` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Application action name\.

<a id='DarkWS.DarkWsBroadcast.DarkWsBroadcast(DarkWS.DarkWsTarget,string,string,System.Nullable_System.Text.Json.JsonElement_).Data'></a>

`Data` [System\.Nullable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')[System\.Text\.Json\.JsonElement](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement 'System\.Text\.Json\.JsonElement')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')

Optional result or notification data\.
### Properties

<a id='DarkWS.DarkWsBroadcast.Action'></a>

## DarkWsBroadcast\.Action Property

Application action name\.

```csharp
public string Action { get; init; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.DarkWsBroadcast.Data'></a>

## DarkWsBroadcast\.Data Property

Optional result or notification data\.

```csharp
public System.Nullable<System.Text.Json.JsonElement> Data { get; init; }
```

#### Property Value
[System\.Nullable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')[System\.Text\.Json\.JsonElement](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement 'System\.Text\.Json\.JsonElement')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')

<a id='DarkWS.DarkWsBroadcast.Target'></a>

## DarkWsBroadcast\.Target Property

Recipient selector\.

```csharp
public DarkWS.DarkWsTarget Target { get; init; }
```

#### Property Value
[DarkWsTarget](DarkWS.DarkWsTarget.md 'DarkWS\.DarkWsTarget')

<a id='DarkWS.DarkWsBroadcast.TargetId'></a>

## DarkWsBroadcast\.TargetId Property

Connection, session, or group key; null for all recipients\.

```csharp
public string? TargetId { get; init; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')