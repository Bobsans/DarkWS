#### [DarkWS\.Client](Overview.md 'Overview')
### [DarkWS\.Client](DarkWS.Client.md 'DarkWS\.Client')

## DarkWsClientErrorEventArgs Class

A background or subscriber error\.

```csharp
public sealed class DarkWsClientErrorEventArgs : System.EventArgs
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.EventArgs](https://learn.microsoft.com/en-us/dotnet/api/system.eventargs 'System\.EventArgs') → DarkWsClientErrorEventArgs
### Constructors

<a id='DarkWS.Client.DarkWsClientErrorEventArgs.DarkWsClientErrorEventArgs(System.Exception)'></a>

## DarkWsClientErrorEventArgs\(Exception\) Constructor

Creates an error notification\.

```csharp
public DarkWsClientErrorEventArgs(System.Exception exception);
```
#### Parameters

<a id='DarkWS.Client.DarkWsClientErrorEventArgs.DarkWsClientErrorEventArgs(System.Exception).exception'></a>

`exception` [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception')
### Properties

<a id='DarkWS.Client.DarkWsClientErrorEventArgs.Exception'></a>

## DarkWsClientErrorEventArgs\.Exception Property

The failure\. Do not log server or application data without appropriate redaction\.

```csharp
public System.Exception Exception { get; }
```

#### Property Value
[System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception')