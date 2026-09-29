#### [DarkWS](Overview.md 'Overview')
### [DarkWS\.Abstractions](DarkWS.Abstractions.md 'DarkWS\.Abstractions')

## IDarkWsContextAccessor Interface

Current message context\. Uninitialized access throws InvalidOperationException; lifecycle hooks receive context explicitly\.

```csharp
public interface IDarkWsContextAccessor
```
### Properties

<a id='DarkWS.Abstractions.IDarkWsContextAccessor.AspNetSession'></a>

## IDarkWsContextAccessor\.AspNetSession Property

Gets ASP\.NET session state when its middleware is installed, otherwise null\.

```csharp
Microsoft.AspNetCore.Http.ISession? AspNetSession { get; }
```

#### Property Value
[Microsoft\.AspNetCore\.Http\.ISession](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.isession 'Microsoft\.AspNetCore\.Http\.ISession')

<a id='DarkWS.Abstractions.IDarkWsContextAccessor.Connection'></a>

## IDarkWsContextAccessor\.Connection Property

Gets the current connection during initialized message handling\.

```csharp
DarkWS.Abstractions.IWebSocketConnection Connection { get; }
```

#### Property Value
[IWebSocketConnection](DarkWS.Abstractions.IWebSocketConnection.md 'DarkWS\.Abstractions\.IWebSocketConnection')

<a id='DarkWS.Abstractions.IDarkWsContextAccessor.ConnectionAborted'></a>

## IDarkWsContextAccessor\.ConnectionAborted Property

Gets the token signaled when the connection stops\. Handlers should observe it during asynchronous work\.

```csharp
System.Threading.CancellationToken ConnectionAborted { get; }
```

#### Property Value
[System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

<a id='DarkWS.Abstractions.IDarkWsContextAccessor.HttpContext'></a>

## IDarkWsContextAccessor\.HttpContext Property

Gets the HTTP upgrade context shared by the connection\.

```csharp
Microsoft.AspNetCore.Http.HttpContext HttpContext { get; }
```

#### Property Value
[Microsoft\.AspNetCore\.Http\.HttpContext](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httpcontext 'Microsoft\.AspNetCore\.Http\.HttpContext')

<a id='DarkWS.Abstractions.IDarkWsContextAccessor.Session'></a>

## IDarkWsContextAccessor\.Session Property

Gets the current session, or null for anonymous access\.

```csharp
DarkWS.Abstractions.IDarkWsSession? Session { get; }
```

#### Property Value
[IDarkWsSession](DarkWS.Abstractions.IDarkWsSession.md 'DarkWS\.Abstractions\.IDarkWsSession')