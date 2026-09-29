#### [DarkWS\.Redis](Overview.md 'Overview')
### [DarkWS\.Redis](DarkWS.Redis.md 'DarkWS\.Redis')

## DarkWsRedisServiceCollectionExtensions Class

Registers Redis broadcast delivery using a host\-owned connection multiplexer\.

```csharp
public static class DarkWsRedisServiceCollectionExtensions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → DarkWsRedisServiceCollectionExtensions
### Methods

<a id='DarkWS.Redis.DarkWsRedisServiceCollectionExtensions.AddDarkWsRedis(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,string)'></a>

## DarkWsRedisServiceCollectionExtensions\.AddDarkWsRedis\(this IServiceCollection, string\) Method

Selects a Redis backplane on a non\-empty literal channel\. Register a host\-owned IConnectionMultiplexer; DarkWS does not dispose it\.

```csharp
public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddDarkWsRedis(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, string channel);
```
#### Parameters

<a id='DarkWS.Redis.DarkWsRedisServiceCollectionExtensions.AddDarkWsRedis(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,string).services'></a>

`services` [Microsoft\.Extensions\.DependencyInjection\.IServiceCollection](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection 'Microsoft\.Extensions\.DependencyInjection\.IServiceCollection')

<a id='DarkWS.Redis.DarkWsRedisServiceCollectionExtensions.AddDarkWsRedis(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,string).channel'></a>

`channel` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[Microsoft\.Extensions\.DependencyInjection\.IServiceCollection](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection 'Microsoft\.Extensions\.DependencyInjection\.IServiceCollection')