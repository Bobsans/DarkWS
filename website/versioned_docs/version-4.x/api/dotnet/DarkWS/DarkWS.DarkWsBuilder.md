#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## DarkWsBuilder Class

Composes handlers, authentication, and message\-scope services after a single AddDarkWs call\.

```csharp
public sealed class DarkWsBuilder
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → DarkWsBuilder
### Properties

<a id='DarkWS.DarkWsBuilder.Services'></a>

## DarkWsBuilder\.Services Property

Gets the service collection being configured\.

```csharp
public Microsoft.Extensions.DependencyInjection.IServiceCollection Services { get; }
```

#### Property Value
[Microsoft\.Extensions\.DependencyInjection\.IServiceCollection](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection 'Microsoft\.Extensions\.DependencyInjection\.IServiceCollection')
### Methods

<a id='DarkWS.DarkWsBuilder.AddAuthenticator_TAuthenticator,TSession_()'></a>

## DarkWsBuilder\.AddAuthenticator\<TAuthenticator,TSession\>\(\) Method

Registers the authenticator and typed session injection in message scopes\. Missing or incompatible sessions throw InvalidOperationException\.

```csharp
public DarkWS.DarkWsBuilder AddAuthenticator<TAuthenticator,TSession>()
    where TAuthenticator : class, DarkWS.Abstractions.IDarkWsAuthenticator
    where TSession : class, DarkWS.Abstractions.IDarkWsSession;
```
#### Type parameters

<a id='DarkWS.DarkWsBuilder.AddAuthenticator_TAuthenticator,TSession_().TAuthenticator'></a>

`TAuthenticator`

<a id='DarkWS.DarkWsBuilder.AddAuthenticator_TAuthenticator,TSession_().TSession'></a>

`TSession`

#### Returns
[DarkWsBuilder](DarkWS.DarkWsBuilder.md 'DarkWS\.DarkWsBuilder')

<a id='DarkWS.DarkWsBuilder.AddHandlersFromAssembly(System.Reflection.Assembly)'></a>

## DarkWsBuilder\.AddHandlersFromAssembly\(Assembly\) Method

Scans concrete handlers and middleware; only actions declared on the concrete handler are registered\.

```csharp
public DarkWS.DarkWsBuilder AddHandlersFromAssembly(System.Reflection.Assembly assembly);
```
#### Parameters

<a id='DarkWS.DarkWsBuilder.AddHandlersFromAssembly(System.Reflection.Assembly).assembly'></a>

`assembly` [System\.Reflection\.Assembly](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.assembly 'System\.Reflection\.Assembly')

#### Returns
[DarkWsBuilder](DarkWS.DarkWsBuilder.md 'DarkWS\.DarkWsBuilder')

<a id='DarkWS.DarkWsBuilder.AddHandlersFromAssemblyContaining_T_()'></a>

## DarkWsBuilder\.AddHandlersFromAssemblyContaining\<T\>\(\) Method

Scans the marker type's assembly for concrete handlers and middleware; invalid or duplicate actions fail registration\.

```csharp
public DarkWS.DarkWsBuilder AddHandlersFromAssemblyContaining<T>();
```
#### Type parameters

<a id='DarkWS.DarkWsBuilder.AddHandlersFromAssemblyContaining_T_().T'></a>

`T`

#### Returns
[DarkWsBuilder](DarkWS.DarkWsBuilder.md 'DarkWS\.DarkWsBuilder')

<a id='DarkWS.DarkWsBuilder.AddScopeInitializer_TInitializer_()'></a>

## DarkWsBuilder\.AddScopeInitializer\<TInitializer\>\(\) Method

Registers initialization of scoped services before every handler invocation\.

```csharp
public DarkWS.DarkWsBuilder AddScopeInitializer<TInitializer>()
    where TInitializer : class, DarkWS.Abstractions.IDarkWsScopeInitializer;
```
#### Type parameters

<a id='DarkWS.DarkWsBuilder.AddScopeInitializer_TInitializer_().TInitializer'></a>

`TInitializer`

#### Returns
[DarkWsBuilder](DarkWS.DarkWsBuilder.md 'DarkWS\.DarkWsBuilder')