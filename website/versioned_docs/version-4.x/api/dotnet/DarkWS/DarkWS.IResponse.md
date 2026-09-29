#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## IResponse Interface

Writes one action result using a correlation context and cancellation token\.

```csharp
public interface IResponse
```

Derived  
↳ [ErrorResponse](DarkWS.ErrorResponse.md 'DarkWS\.ErrorResponse')  
↳ [ErrorResponse&lt;T&gt;](DarkWS.ErrorResponse_T_.md 'DarkWS\.ErrorResponse\<T\>')  
↳ [SuccessResponse](DarkWS.SuccessResponse.md 'DarkWS\.SuccessResponse')  
↳ [SuccessResponse&lt;T&gt;](DarkWS.SuccessResponse_T_.md 'DarkWS\.SuccessResponse\<T\>')
### Methods

<a id='DarkWS.IResponse.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken)'></a>

## IResponse\.WriteResultAsync\(ResponseContext, CancellationToken\) Method

Writes this result using the supplied correlation context and cancellation token\.

```csharp
System.Threading.Tasks.Task WriteResultAsync(DarkWS.ResponseContext context, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.IResponse.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken).context'></a>

`context` [ResponseContext](DarkWS.ResponseContext.md 'DarkWS\.ResponseContext')

<a id='DarkWS.IResponse.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')