#### [DarkWS\.Redis](Overview.md 'Overview')
### [DarkWS\.Redis](DarkWS.Redis.md 'DarkWS\.Redis')

## RedisConfiguration Class

Compatibility entry point for legacy static Redis registration calls\.

```csharp
public static class RedisConfiguration
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → RedisConfiguration
### Methods

<a id='DarkWS.Redis.RedisConfiguration.AddDarkWsRedis(Microsoft.Extensions.DependencyInjection.IServiceCollection,string)'></a>

## RedisConfiguration\.AddDarkWsRedis\(IServiceCollection, string\) Method

Forwards the legacy static Redis backplane registration call\.

```csharp
public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddDarkWsRedis(Microsoft.Extensions.DependencyInjection.IServiceCollection services, string channel);
```
#### Parameters

<a id='DarkWS.Redis.RedisConfiguration.AddDarkWsRedis(Microsoft.Extensions.DependencyInjection.IServiceCollection,string).services'></a>

`services` [Microsoft\.Extensions\.DependencyInjection\.IServiceCollection](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection 'Microsoft\.Extensions\.DependencyInjection\.IServiceCollection')

<a id='DarkWS.Redis.RedisConfiguration.AddDarkWsRedis(Microsoft.Extensions.DependencyInjection.IServiceCollection,string).channel'></a>

`channel` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

#### Returns
[Microsoft\.Extensions\.DependencyInjection\.IServiceCollection](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection 'Microsoft\.Extensions\.DependencyInjection\.IServiceCollection')