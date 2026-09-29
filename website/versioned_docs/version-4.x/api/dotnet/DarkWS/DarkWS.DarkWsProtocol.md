#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## DarkWsProtocol Class

Reserved identifiers in the DarkWS wire protocol\.

```csharp
public static class DarkWsProtocol
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → DarkWsProtocol
### Fields

<a id='DarkWS.DarkWsProtocol.BroadcastId'></a>

## DarkWsProtocol\.BroadcastId Field

Identifies an unsolicited broadcast; clients must not use it as a request id\.

```csharp
public const string BroadcastId = "@";
```

#### Field Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.DarkWsProtocol.LegacyAuthenticationId'></a>

## DarkWsProtocol\.LegacyAuthenticationId Field

Reserved legacy acknowledgement id\. Current system replies are plain text and do not emit this id\.

```csharp
public const string LegacyAuthenticationId = "@auth";
```

#### Field Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')