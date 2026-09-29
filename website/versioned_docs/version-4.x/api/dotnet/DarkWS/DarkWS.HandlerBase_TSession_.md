#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## HandlerBase\<TSession\> Class

Base for per\-message handlers\. Context\-dependent members are available only during action invocation\.

```csharp
public abstract class HandlerBase<TSession> : DarkWS.HandlerBase
    where TSession : class, DarkWS.Abstractions.IDarkWsSession
```
#### Type parameters

<a id='DarkWS.HandlerBase_TSession_.TSession'></a>

`TSession`

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [HandlerBase](DarkWS.HandlerBase.md 'DarkWS\.HandlerBase') → HandlerBase\<TSession\>
### Properties

<a id='DarkWS.HandlerBase_TSession_.Session'></a>

## HandlerBase\<TSession\>\.Session Property

Gets the required handler session\. Throws InvalidOperationException for an anonymous connection or incompatible typed session\.

```csharp
protected TSession Session { protected get; }
```

#### Property Value
[TSession](DarkWS.HandlerBase_TSession_.md#DarkWS.HandlerBase_TSession_.TSession 'DarkWS\.HandlerBase\<TSession\>\.TSession')