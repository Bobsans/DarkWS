#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## SuccessResponse\<T\> Class

Writes a successful response with optional typed result data\.

```csharp
public sealed class SuccessResponse<T> : DarkWS.IResponse
```
#### Type parameters

<a id='DarkWS.SuccessResponse_T_.T'></a>

`T`

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → SuccessResponse\<T\>

Implements [IResponse](DarkWS.IResponse.md 'DarkWS\.IResponse')
### Constructors

<a id='DarkWS.SuccessResponse_T_.SuccessResponse(T)'></a>

## SuccessResponse\(T\) Constructor

Writes a successful response with optional typed result data\.

```csharp
public SuccessResponse(T data);
```
#### Parameters

<a id='DarkWS.SuccessResponse_T_.SuccessResponse(T).data'></a>

`data` [T](DarkWS.SuccessResponse_T_.md#DarkWS.SuccessResponse_T_.T 'DarkWS\.SuccessResponse\<T\>\.T')

Result or message data\.
### Methods

<a id='DarkWS.SuccessResponse_T_.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken)'></a>

## SuccessResponse\<T\>\.WriteResultAsync\(ResponseContext, CancellationToken\) Method

Writes this result using the supplied correlation context and cancellation token\.

```csharp
public System.Threading.Tasks.Task WriteResultAsync(DarkWS.ResponseContext context, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.SuccessResponse_T_.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken).context'></a>

`context` [ResponseContext](DarkWS.ResponseContext.md 'DarkWS\.ResponseContext')

<a id='DarkWS.SuccessResponse_T_.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

Implements [WriteResultAsync\(ResponseContext, CancellationToken\)](DarkWS.IResponse.md#DarkWS.IResponse.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken) 'DarkWS\.IResponse\.WriteResultAsync\(DarkWS\.ResponseContext, System\.Threading\.CancellationToken\)')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')