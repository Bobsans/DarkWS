#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## DarkWsServiceCollectionExtensions Class

Registers DarkWS services and validated options\.

```csharp
public static class DarkWsServiceCollectionExtensions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → DarkWsServiceCollectionExtensions
### Methods

<a id='DarkWS.DarkWsServiceCollectionExtensions.AddDarkWs(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,System.Action_DarkWS.DarkWsOptions_)'></a>

## DarkWsServiceCollectionExtensions\.AddDarkWs\(this IServiceCollection, Action\<DarkWsOptions\>\) Method

Registers DarkWS once and composes options\. Invalid settings fail on resolution or host startup; duplicate registration throws InvalidOperationException\.

```csharp
public static DarkWS.DarkWsBuilder AddDarkWs(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, System.Action<DarkWS.DarkWsOptions>? configure=null);
```
#### Parameters

<a id='DarkWS.DarkWsServiceCollectionExtensions.AddDarkWs(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,System.Action_DarkWS.DarkWsOptions_).services'></a>

`services` [Microsoft\.Extensions\.DependencyInjection\.IServiceCollection](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection 'Microsoft\.Extensions\.DependencyInjection\.IServiceCollection')

<a id='DarkWS.DarkWsServiceCollectionExtensions.AddDarkWs(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,System.Action_DarkWS.DarkWsOptions_).configure'></a>

`configure` [System\.Action&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')[DarkWsOptions](DarkWS.DarkWsOptions.md 'DarkWS\.DarkWsOptions')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')

#### Returns
[DarkWsBuilder](DarkWS.DarkWsBuilder.md 'DarkWS\.DarkWsBuilder')