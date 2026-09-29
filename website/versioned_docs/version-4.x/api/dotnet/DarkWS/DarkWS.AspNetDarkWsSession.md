#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## AspNetDarkWsSession Class

Default session for an authenticated ASP\.NET principal, without broadcast groups\.

```csharp
public sealed record AspNetDarkWsSession : DarkWS.Abstractions.IDarkWsSession, System.IEquatable<DarkWS.AspNetDarkWsSession>
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → AspNetDarkWsSession

Implements [IDarkWsSession](DarkWS.Abstractions.IDarkWsSession.md 'DarkWS\.Abstractions\.IDarkWsSession'), [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[AspNetDarkWsSession](DarkWS.AspNetDarkWsSession.md 'DarkWS\.AspNetDarkWsSession')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')
### Constructors

<a id='DarkWS.AspNetDarkWsSession.AspNetDarkWsSession(string,System.Security.Claims.ClaimsPrincipal)'></a>

## AspNetDarkWsSession\(string, ClaimsPrincipal\) Constructor

Default session for an authenticated ASP\.NET principal, without broadcast groups\.

```csharp
public AspNetDarkWsSession(string Id, System.Security.Claims.ClaimsPrincipal User);
```
#### Parameters

<a id='DarkWS.AspNetDarkWsSession.AspNetDarkWsSession(string,System.Security.Claims.ClaimsPrincipal).Id'></a>

`Id` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Correlation or identity key\.

<a id='DarkWS.AspNetDarkWsSession.AspNetDarkWsSession(string,System.Security.Claims.ClaimsPrincipal).User'></a>

`User` [System\.Security\.Claims\.ClaimsPrincipal](https://learn.microsoft.com/en-us/dotnet/api/system.security.claims.claimsprincipal 'System\.Security\.Claims\.ClaimsPrincipal')

Authenticated principal\.
### Properties

<a id='DarkWS.AspNetDarkWsSession.Groups'></a>

## AspNetDarkWsSession\.Groups Property

Gets broadcast groups; storage snapshots membership on add or re\-authentication\.

```csharp
public System.Collections.Generic.IReadOnlyCollection<string> Groups { get; }
```

Implements [Groups](DarkWS.Abstractions.IDarkWsSession.md#DarkWS.Abstractions.IDarkWsSession.Groups 'DarkWS\.Abstractions\.IDarkWsSession\.Groups')

#### Property Value
[System\.Collections\.Generic\.IReadOnlyCollection&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')

<a id='DarkWS.AspNetDarkWsSession.Id'></a>

## AspNetDarkWsSession\.Id Property

Correlation or identity key\.

```csharp
public string Id { get; init; }
```

Implements [Id](DarkWS.Abstractions.IDarkWsSession.md#DarkWS.Abstractions.IDarkWsSession.Id 'DarkWS\.Abstractions\.IDarkWsSession\.Id')

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.AspNetDarkWsSession.User'></a>

## AspNetDarkWsSession\.User Property

Authenticated principal\.

```csharp
public System.Security.Claims.ClaimsPrincipal User { get; init; }
```

Implements [User](DarkWS.Abstractions.IDarkWsSession.md#DarkWS.Abstractions.IDarkWsSession.User 'DarkWS\.Abstractions\.IDarkWsSession\.User')

#### Property Value
[System\.Security\.Claims\.ClaimsPrincipal](https://learn.microsoft.com/en-us/dotnet/api/system.security.claims.claimsprincipal 'System\.Security\.Claims\.ClaimsPrincipal')