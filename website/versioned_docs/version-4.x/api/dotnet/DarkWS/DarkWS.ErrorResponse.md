#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## ErrorResponse Class

Writes an error code and optional typed details to the client\.

```csharp
public class ErrorResponse : DarkWS.IResponse
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → ErrorResponse

Implements [IResponse](DarkWS.IResponse.md 'DarkWS\.IResponse')
### Constructors

<a id='DarkWS.ErrorResponse.ErrorResponse(string)'></a>

## ErrorResponse\(string\) Constructor

Writes an error code and optional typed details to the client\.

```csharp
public ErrorResponse(string error);
```
#### Parameters

<a id='DarkWS.ErrorResponse.ErrorResponse(string).error'></a>

`error` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Stable error code\.
### Methods

<a id='DarkWS.ErrorResponse.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken)'></a>

## ErrorResponse\.WriteResultAsync\(ResponseContext, CancellationToken\) Method

Writes this result using the supplied correlation context and cancellation token\.

```csharp
public System.Threading.Tasks.Task WriteResultAsync(DarkWS.ResponseContext context, System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken));
```
#### Parameters

<a id='DarkWS.ErrorResponse.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken).context'></a>

`context` [ResponseContext](DarkWS.ResponseContext.md 'DarkWS\.ResponseContext')

<a id='DarkWS.ErrorResponse.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken).cancellationToken'></a>

`cancellationToken` [System\.Threading\.CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken 'System\.Threading\.CancellationToken')

Implements [WriteResultAsync\(ResponseContext, CancellationToken\)](DarkWS.IResponse.md#DarkWS.IResponse.WriteResultAsync(DarkWS.ResponseContext,System.Threading.CancellationToken) 'DarkWS\.IResponse\.WriteResultAsync\(DarkWS\.ResponseContext, System\.Threading\.CancellationToken\)')

#### Returns
[System\.Threading\.Tasks\.Task](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task 'System\.Threading\.Tasks\.Task')