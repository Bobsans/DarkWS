#### [DarkWS\.Client\.DependencyInjection](Overview.md 'Overview')
### [Microsoft\.Extensions\.DependencyInjection](Microsoft.Extensions.DependencyInjection.md 'Microsoft\.Extensions\.DependencyInjection')

## DarkWsClientServiceCollectionExtensions Class

Optional registration of a single shared DarkWS client session\.

```csharp
public static class DarkWsClientServiceCollectionExtensions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → DarkWsClientServiceCollectionExtensions
### Methods

<a id='Microsoft.Extensions.DependencyInjection.DarkWsClientServiceCollectionExtensions.AddDarkWsClient(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,System.Action_DarkWS.Client.DarkWsClientOptions_)'></a>

## DarkWsClientServiceCollectionExtensions\.AddDarkWsClient\(this IServiceCollection, Action\<DarkWsClientOptions\>\) Method

Registers a lazy singleton IDarkWsClient owned by the container\. Duplicate registration is rejected\.

```csharp
public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddDarkWsClient(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, System.Action<DarkWS.Client.DarkWsClientOptions> configure);
```
#### Parameters

<a id='Microsoft.Extensions.DependencyInjection.DarkWsClientServiceCollectionExtensions.AddDarkWsClient(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,System.Action_DarkWS.Client.DarkWsClientOptions_).services'></a>

`services` [Microsoft\.Extensions\.DependencyInjection\.IServiceCollection](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection 'Microsoft\.Extensions\.DependencyInjection\.IServiceCollection')

<a id='Microsoft.Extensions.DependencyInjection.DarkWsClientServiceCollectionExtensions.AddDarkWsClient(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,System.Action_DarkWS.Client.DarkWsClientOptions_).configure'></a>

`configure` [System\.Action&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')[DarkWS\.Client\.DarkWsClientOptions](https://learn.microsoft.com/en-us/dotnet/api/darkws.client.darkwsclientoptions 'DarkWS\.Client\.DarkWsClientOptions')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')

#### Returns
[Microsoft\.Extensions\.DependencyInjection\.IServiceCollection](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection 'Microsoft\.Extensions\.DependencyInjection\.IServiceCollection')

<a id='Microsoft.Extensions.DependencyInjection.DarkWsClientServiceCollectionExtensions.AddDarkWsClient(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,System.Action_System.IServiceProvider,DarkWS.Client.DarkWsClientOptions_)'></a>

## DarkWsClientServiceCollectionExtensions\.AddDarkWsClient\(this IServiceCollection, Action\<IServiceProvider,DarkWsClientOptions\>\) Method

Registers a lazy singleton with access to application services\. Do not capture scoped services\.

```csharp
public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddDarkWsClient(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, System.Action<System.IServiceProvider,DarkWS.Client.DarkWsClientOptions> configure);
```
#### Parameters

<a id='Microsoft.Extensions.DependencyInjection.DarkWsClientServiceCollectionExtensions.AddDarkWsClient(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,System.Action_System.IServiceProvider,DarkWS.Client.DarkWsClientOptions_).services'></a>

`services` [Microsoft\.Extensions\.DependencyInjection\.IServiceCollection](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection 'Microsoft\.Extensions\.DependencyInjection\.IServiceCollection')

<a id='Microsoft.Extensions.DependencyInjection.DarkWsClientServiceCollectionExtensions.AddDarkWsClient(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,System.Action_System.IServiceProvider,DarkWS.Client.DarkWsClientOptions_).configure'></a>

`configure` [System\.Action&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-2 'System\.Action\`2')[System\.IServiceProvider](https://learn.microsoft.com/en-us/dotnet/api/system.iserviceprovider 'System\.IServiceProvider')[,](https://learn.microsoft.com/en-us/dotnet/api/system.action-2 'System\.Action\`2')[DarkWS\.Client\.DarkWsClientOptions](https://learn.microsoft.com/en-us/dotnet/api/darkws.client.darkwsclientoptions 'DarkWS\.Client\.DarkWsClientOptions')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-2 'System\.Action\`2')

#### Returns
[Microsoft\.Extensions\.DependencyInjection\.IServiceCollection](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection 'Microsoft\.Extensions\.DependencyInjection\.IServiceCollection')