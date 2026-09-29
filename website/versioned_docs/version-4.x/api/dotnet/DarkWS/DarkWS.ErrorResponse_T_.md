#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## ErrorResponse\<T\> Class

Writes an error code and optional typed details to the client\.

```csharp
public sealed class ErrorResponse<T> : DarkWS.IResponse
```
#### Type parameters

<a id='DarkWS.ErrorResponse_T_.T'></a>

`T`

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → ErrorResponse\<T\>

Implements [IResponse](DarkWS.IResponse.md 'DarkWS\.IResponse')
### Constructors

<a id='DarkWS.ErrorResponse_T_.ErrorResponse(string,T)'></a>

## ErrorResponse\(string, T\) Constructor

Writes an error code and optional typed details to the client\.

```csharp
public ErrorResponse(string error, T details);
```
#### Parameters

<a id='DarkWS.ErrorResponse_T_.ErrorResponse(string,T).error'></a>

`error` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Stable error code\.

<a id='DarkWS.ErrorResponse_T_.ErrorResponse(string,T).details'></a>

`details` [T](DarkWS.ErrorResponse_T_.md#DarkWS.ErrorResponse_T_.T 'DarkWS\.ErrorResponse\<T\>\.T')

Typed details sent to the client\.
### Methods

<a id='DarkWS.ErrorResponse_T_.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken)'></a>

## ErrorResponse\<T\>\.WriteResultAsync\(ResponseContext, CancellationToken\) Method

Writes this result using the supplied correlation context and cancellation token\.

```csharp
public System.Threading.Tasks.Task WriteResultAsync(DarkWS.ResponseContext context, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.ErrorResponse_T_.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken).context'></a>

`context` [ResponseContext](DarkWS.ResponseContext.md 'DarkWS\.ResponseContext')

<a id='DarkWS.ErrorResponse_T_.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

Implements [WriteResultAsync\(ResponseContext, CancellationToken\)](DarkWS.IResponse.md#DarkWS.IResponse.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken) 'DarkWS\.IResponse\.WriteResultAsync\(DarkWS\.ResponseContext, System\.Threading\.CancellationToken\)')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')