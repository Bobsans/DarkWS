#### [DarkWS](Overview.md 'Overview')
### [DarkWS\.Abstractions](DarkWS.Abstractions.md 'DarkWS\.Abstractions')

## IDarkWsAuthenticator Interface

Resolves a session for a request or replacement token\. Return null on rejection; expiry remains application policy\.

```csharp
public interface IDarkWsAuthenticator
```
### Methods

<a id='DarkWS.Abstractions.IDarkWsAuthenticator.AuthenticateAsync(Microsoft.AspNetCore.Http.HttpContext,string,System.Threading.CancellationToken)'></a>

## IDarkWsAuthenticator\.AuthenticateAsync\(HttpContext, string, CancellationToken\) Method

Resolves a session for an HTTP request and optional token, or null on rejection\.

```csharp
System.Threading.Tasks.ValueTask<DarkWS.Abstractions.IDarkWsSession?> AuthenticateAsync(Microsoft.AspNetCore.Http.HttpContext context, string? token, System.Threading.CancellationToken cancellationToken);
```
#### Parameters

<a id='DarkWS.Abstractions.IDarkWsAuthenticator.AuthenticateAsync(Microsoft.AspNetCore.Http.HttpContext,string,System.Threading.CancellationToken).context'></a>

`context` [Microsoft\.AspNetCore\.Http\.HttpContext](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httpcontext 'Microsoft\.AspNetCore\.Http\.HttpContext')

<a id='DarkWS.Abstractions.IDarkWsAuthenticator.AuthenticateAsync(Microsoft.AspNetCore.Http.HttpContext,string,System.Threading.CancellationToken).token'></a>

`token` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.Abstractions.IDarkWsAuthenticator.AuthenticateAsync(Microsoft.AspNetCore.Http.HttpContext,string,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.ValueTask&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1 'System\.Threading\.Tasks\.ValueTask\`1')[IDarkWsSession](DarkWS.Abstractions.IDarkWsSession.md 'DarkWS\.Abstractions\.IDarkWsSession')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1 'System\.Threading\.Tasks\.ValueTask\`1')