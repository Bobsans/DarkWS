#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## ErrorResponseException\<T\> Class

Controlled handler exception translated to an error code and optional typed details\.

```csharp
public class ErrorResponseException<T> : DarkWS.DarkWsException
```
#### Type parameters

<a id='DarkWS.ErrorResponseException_T_.T'></a>

`T`

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') → [DarkWsException](DarkWS.DarkWsException.md 'DarkWS\.DarkWsException') → ErrorResponseException\<T\>
### Constructors

<a id='DarkWS.ErrorResponseException_T_.ErrorResponseException(string,T)'></a>

## ErrorResponseException\(string, T\) Constructor

Creates a controlled error with typed details sent to the client\.

```csharp
public ErrorResponseException(string error, T details);
```
#### Parameters

<a id='DarkWS.ErrorResponseException_T_.ErrorResponseException(string,T).error'></a>

`error` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.ErrorResponseException_T_.ErrorResponseException(string,T).details'></a>

`details` [T](DarkWS.ErrorResponseException_T_.md#DarkWS.ErrorResponseException_T_.T 'DarkWS\.ErrorResponseException\<T\>\.T')

<a id='DarkWS.ErrorResponseException_T_.ErrorResponseException(string,T,System.Exception)'></a>

## ErrorResponseException\(string, T, Exception\) Constructor

Creates a controlled error with typed client details and its original cause\.

```csharp
public ErrorResponseException(string error, T details, System.Exception? innerException);
```
#### Parameters

<a id='DarkWS.ErrorResponseException_T_.ErrorResponseException(string,T,System.Exception).error'></a>

`error` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.ErrorResponseException_T_.ErrorResponseException(string,T,System.Exception).details'></a>

`details` [T](DarkWS.ErrorResponseException_T_.md#DarkWS.ErrorResponseException_T_.T 'DarkWS\.ErrorResponseException\<T\>\.T')

<a id='DarkWS.ErrorResponseException_T_.ErrorResponseException(string,T,System.Exception).innerException'></a>

`innerException` [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception')
### Methods

<a id='DarkWS.ErrorResponseException_T_.GetResponse()'></a>

## ErrorResponseException\<T\>\.GetResponse\(\) Method

Returns the controlled protocol response associated with this exception\.

```csharp
public override DarkWS.IResponse GetResponse();
```

#### Returns
[IResponse](DarkWS.IResponse.md 'DarkWS\.IResponse')