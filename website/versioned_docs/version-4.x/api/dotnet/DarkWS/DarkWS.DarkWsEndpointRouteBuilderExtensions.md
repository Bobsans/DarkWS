#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## DarkWsEndpointRouteBuilderExtensions Class

Maps DarkWS endpoints in the ASP\.NET Core request pipeline\.

```csharp
public static class DarkWsEndpointRouteBuilderExtensions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → DarkWsEndpointRouteBuilderExtensions
### Methods

<a id='DarkWS.DarkWsEndpointRouteBuilderExtensions.MapDarkWs(thisMicrosoft.AspNetCore.Routing.IEndpointRouteBuilder,string)'></a>

## DarkWsEndpointRouteBuilderExtensions\.MapDarkWs\(this IEndpointRouteBuilder, string\) Method

Maps an authenticated WebSocket endpoint with resource limits and liveness detection\. Requires UseWebSockets\.

```csharp
public static Microsoft.AspNetCore.Builder.IEndpointConventionBuilder MapDarkWs(this Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints, string pattern="/ws");
```
#### Parameters

<a id='DarkWS.DarkWsEndpointRouteBuilderExtensions.MapDarkWs(thisMicrosoft.AspNetCore.Routing.IEndpointRouteBuilder,string).endpoints'></a>

`endpoints` [Microsoft\.AspNetCore\.Routing\.IEndpointRouteBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.routing.iendpointroutebuilder 'Microsoft\.AspNetCore\.Routing\.IEndpointRouteBuilder')

<a id='DarkWS.DarkWsEndpointRouteBuilderExtensions.MapDarkWs(thisMicrosoft.AspNetCore.Routing.IEndpointRouteBuilder,string).pattern'></a>

`pattern` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[Microsoft\.AspNetCore\.Builder\.IEndpointConventionBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.iendpointconventionbuilder 'Microsoft\.AspNetCore\.Builder\.IEndpointConventionBuilder')