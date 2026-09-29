#### [DarkWS\.Client](Overview.md 'Overview')
### [DarkWS\.Client](DarkWS.Client.md 'DarkWS\.Client')

## DarkWsTimeoutException Class

A bounded client operation expired\.

```csharp
public sealed class DarkWsTimeoutException : System.TimeoutException
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') → [System\.SystemException](https://learn.microsoft.com/en-us/dotnet/api/system.systemexception 'System\.SystemException') → [System\.TimeoutException](https://learn.microsoft.com/en-us/dotnet/api/system.timeoutexception 'System\.TimeoutException') → DarkWsTimeoutException
### Constructors

<a id='DarkWS.Client.DarkWsTimeoutException.DarkWsTimeoutException(DarkWS.Client.DarkWsTimeoutStage)'></a>

## DarkWsTimeoutException\(DarkWsTimeoutStage\) Constructor

Creates a timeout for the given stage\.

```csharp
public DarkWsTimeoutException(DarkWS.Client.DarkWsTimeoutStage stage);
```
#### Parameters

<a id='DarkWS.Client.DarkWsTimeoutException.DarkWsTimeoutException(DarkWS.Client.DarkWsTimeoutStage).stage'></a>

`stage` [DarkWsTimeoutStage](DarkWS.Client.DarkWsTimeoutStage.md 'DarkWS\.Client\.DarkWsTimeoutStage')
### Properties

<a id='DarkWS.Client.DarkWsTimeoutException.Stage'></a>

## DarkWsTimeoutException\.Stage Property

The expired stage\.

```csharp
public DarkWS.Client.DarkWsTimeoutStage Stage { get; }
```

#### Property Value
[DarkWsTimeoutStage](DarkWS.Client.DarkWsTimeoutStage.md 'DarkWS\.Client\.DarkWsTimeoutStage')