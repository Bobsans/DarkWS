#### [DarkWS](Overview.md 'Overview')
### [DarkWS\.Abstractions](DarkWS.Abstractions.md 'DarkWS\.Abstractions')

## IDarkWsSession Interface

Application session identity, principal, and groups\. Membership is snapshotted on add or re\-authentication\.

```csharp
public interface IDarkWsSession
```

Derived  
↳ [AspNetDarkWsSession](DarkWS.AspNetDarkWsSession.md 'DarkWS\.AspNetDarkWsSession')
### Properties

<a id='DarkWS.Abstractions.IDarkWsSession.Groups'></a>

## IDarkWsSession\.Groups Property

Gets broadcast groups; storage snapshots membership on add or re\-authentication\.

```csharp
System.Collections.Generic.IReadOnlyCollection<string> Groups { get; }
```

#### Property Value
[System\.Collections\.Generic\.IReadOnlyCollection&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')

<a id='DarkWS.Abstractions.IDarkWsSession.Id'></a>

## IDarkWsSession\.Id Property

Gets the stable identity for connection or session targeting\.

```csharp
string Id { get; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IDarkWsSession.User'></a>

## IDarkWsSession\.User Property

Gets the principal used for action authorization\.

```csharp
System.Security.Claims.ClaimsPrincipal User { get; }
```

#### Property Value
[System\.Security\.Claims\.ClaimsPrincipal](https://learn.microsoft.com/en-us/dotnet/api/system.security.claims.claimsprincipal 'System\.Security\.Claims\.ClaimsPrincipal')