#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## DarkWsMiddleware Class

Connection lifecycle hooks\. Use the supplied context; an injected message context is uninitialized in this scope\.

```csharp
public abstract class DarkWsMiddleware
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → DarkWsMiddleware
### Methods

<a id='DarkWS.DarkWsMiddleware.OnAuthenticatedAsync(DarkWS.Abstractions.IDarkWsContextAccessor,DarkWS.Abstractions.IDarkWsSession)'></a>

## DarkWsMiddleware\.OnAuthenticatedAsync\(IDarkWsContextAccessor, IDarkWsSession\) Method

Runs after session replacement, failed re\-authentication, or logout\. The current session can be null; previousSession identifies the replaced session\.

```csharp
public virtual System.Threading.Tasks.Task OnAuthenticatedAsync(DarkWS.Abstractions.IDarkWsContextAccessor context, DarkWS.Abstractions.IDarkWsSession? previousSession);
```
#### Parameters

<a id='DarkWS.DarkWsMiddleware.OnAuthenticatedAsync(DarkWS.Abstractions.IDarkWsContextAccessor,DarkWS.Abstractions.IDarkWsSession).context'></a>

`context` [IDarkWsContextAccessor](DarkWS.Abstractions.IDarkWsContextAccessor.md 'DarkWS\.Abstractions\.IDarkWsContextAccessor')

<a id='DarkWS.DarkWsMiddleware.OnAuthenticatedAsync(DarkWS.Abstractions.IDarkWsContextAccessor,DarkWS.Abstractions.IDarkWsSession).previousSession'></a>

`previousSession` [IDarkWsSession](DarkWS.Abstractions.IDarkWsSession.md 'DarkWS\.Abstractions\.IDarkWsSession')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.DarkWsMiddleware.OnCloseAsync(DarkWS.Abstractions.IDarkWsContextAccessor)'></a>

## DarkWsMiddleware\.OnCloseAsync\(IDarkWsContextAccessor\) Method

Runs after removal from storage\. Finish promptly and observe cancellation to allow resource cleanup\.

```csharp
public virtual System.Threading.Tasks.Task OnCloseAsync(DarkWS.Abstractions.IDarkWsContextAccessor context);
```
#### Parameters

<a id='DarkWS.DarkWsMiddleware.OnCloseAsync(DarkWS.Abstractions.IDarkWsContextAccessor).context'></a>

`context` [IDarkWsContextAccessor](DarkWS.Abstractions.IDarkWsContextAccessor.md 'DarkWS\.Abstractions\.IDarkWsContextAccessor')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')

<a id='DarkWS.DarkWsMiddleware.OnOpenAsync(DarkWS.Abstractions.IDarkWsContextAccessor)'></a>

## DarkWsMiddleware\.OnOpenAsync\(IDarkWsContextAccessor\) Method

Runs after connection registration with an initialized context\.

```csharp
public virtual System.Threading.Tasks.Task OnOpenAsync(DarkWS.Abstractions.IDarkWsContextAccessor context);
```
#### Parameters

<a id='DarkWS.DarkWsMiddleware.OnOpenAsync(DarkWS.Abstractions.IDarkWsContextAccessor).context'></a>

`context` [IDarkWsContextAccessor](DarkWS.Abstractions.IDarkWsContextAccessor.md 'DarkWS\.Abstractions\.IDarkWsContextAccessor')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')