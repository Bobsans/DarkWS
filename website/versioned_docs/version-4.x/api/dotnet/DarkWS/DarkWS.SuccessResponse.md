#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## SuccessResponse Class

Writes a successful response with optional typed result data\.

```csharp
public sealed class SuccessResponse : DarkWS.IResponse
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → SuccessResponse

Implements [IResponse](DarkWS.IResponse.md 'DarkWS\.IResponse')
### Methods

<a id='DarkWS.SuccessResponse.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken)'></a>

## SuccessResponse\.WriteResultAsync\(ResponseContext, CancellationToken\) Method

Writes this result using the supplied correlation context and cancellation token\.

```csharp
public System.Threading.Tasks.Task WriteResultAsync(DarkWS.ResponseContext context, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.SuccessResponse.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken).context'></a>

`context` [ResponseContext](DarkWS.ResponseContext.md 'DarkWS\.ResponseContext')

<a id='DarkWS.SuccessResponse.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

Implements [WriteResultAsync\(ResponseContext, CancellationToken\)](DarkWS.IResponse.md#DarkWS.IResponse.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken) 'DarkWS\.IResponse\.WriteResultAsync\(DarkWS\.ResponseContext, System\.Threading\.CancellationToken\)')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')