#### [DarkWS](Overview.md 'Overview')
### [DarkWS\.Abstractions](DarkWS.Abstractions.md 'DarkWS\.Abstractions')

## IDarkWsScopeInitializer Interface

Initializes scoped application services before each handler invocation\.

```csharp
public interface IDarkWsScopeInitializer
```
### Methods

<a id='DarkWS.Abstractions.IDarkWsScopeInitializer.InitializeAsync(System.IServiceProvider,DarkWS.Abstractions.IDarkWsContextAccessor,System.Threading.CancellationToken)'></a>

## IDarkWsScopeInitializer\.InitializeAsync\(IServiceProvider, IDarkWsContextAccessor, CancellationToken\) Method

Initializes application services in the current message scope before handler invocation\.

```csharp
System.Threading.Tasks.ValueTask InitializeAsync(System.IServiceProvider scopedServices, DarkWS.Abstractions.IDarkWsContextAccessor context, System.Threading.CancellationToken cancellationToken);
```
#### Parameters

<a id='DarkWS.Abstractions.IDarkWsScopeInitializer.InitializeAsync(System.IServiceProvider,DarkWS.Abstractions.IDarkWsContextAccessor,System.Threading.CancellationToken).scopedServices'></a>

`scopedServices` [System\.IServiceProvider](https://learn.microsoft.com/en-us/dotnet/api/system.iserviceprovider 'System\.IServiceProvider')

<a id='DarkWS.Abstractions.IDarkWsScopeInitializer.InitializeAsync(System.IServiceProvider,DarkWS.Abstractions.IDarkWsContextAccessor,System.Threading.CancellationToken).context'></a>

`context` [IDarkWsContextAccessor](DarkWS.Abstractions.IDarkWsContextAccessor.md 'DarkWS\.Abstractions\.IDarkWsContextAccessor')

<a id='DarkWS.Abstractions.IDarkWsScopeInitializer.InitializeAsync(System.IServiceProvider,DarkWS.Abstractions.IDarkWsContextAccessor,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.ValueTask](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask 'System\.Threading\.Tasks\.ValueTask')