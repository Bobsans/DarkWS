#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## Configuration Class

Compatibility entry points; use the dedicated service and endpoint extension classes\.

```csharp
public static class Configuration
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → Configuration
### Methods

<a id='DarkWS.Configuration.AddDarkWs(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action_DarkWS.DarkWsOptions_)'></a>

## Configuration\.AddDarkWs\(IServiceCollection, Action\<DarkWsOptions\>\) Method

Forwards the legacy static service registration call\.

```csharp
public static DarkWS.DarkWsBuilder AddDarkWs(Microsoft.Extensions.DependencyInjection.IServiceCollection services, System.Action<DarkWS.DarkWsOptions>? configure=null);
```
#### Parameters

<a id='DarkWS.Configuration.AddDarkWs(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action_DarkWS.DarkWsOptions_).services'></a>

`services` [Microsoft\.Extensions\.DependencyInjection\.IServiceCollection](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection 'Microsoft\.Extensions\.DependencyInjection\.IServiceCollection')

<a id='DarkWS.Configuration.AddDarkWs(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action_DarkWS.DarkWsOptions_).configure'></a>

`configure` [System\.Action&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')[DarkWsOptions](DarkWS.DarkWsOptions.md 'DarkWS\.DarkWsOptions')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')

#### Returns
[DarkWsBuilder](DarkWS.DarkWsBuilder.md 'DarkWS\.DarkWsBuilder')

<a id='DarkWS.Configuration.MapDarkWs(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder,string)'></a>

## Configuration\.MapDarkWs\(IEndpointRouteBuilder, string\) Method

Forwards the legacy static endpoint registration call\.

```csharp
public static Microsoft.AspNetCore.Builder.IEndpointConventionBuilder MapDarkWs(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints, string pattern="/ws");
```
#### Parameters

<a id='DarkWS.Configuration.MapDarkWs(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder,string).endpoints'></a>

`endpoints` [Microsoft\.AspNetCore\.Routing\.IEndpointRouteBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.routing.iendpointroutebuilder 'Microsoft\.AspNetCore\.Routing\.IEndpointRouteBuilder')

<a id='DarkWS.Configuration.MapDarkWs(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder,string).pattern'></a>

`pattern` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[Microsoft\.AspNetCore\.Builder\.IEndpointConventionBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.iendpointconventionbuilder 'Microsoft\.AspNetCore\.Builder\.IEndpointConventionBuilder')