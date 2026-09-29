#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## ErrorResponseException Class

Controlled handler exception translated to an error code and optional typed details\.

```csharp
public class ErrorResponseException : DarkWS.DarkWsException
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') → [DarkWsException](DarkWS.DarkWsException.md 'DarkWS\.DarkWsException') → ErrorResponseException
### Constructors

<a id='DarkWS.ErrorResponseException.ErrorResponseException(string)'></a>

## ErrorResponseException\(string\) Constructor

Creates a controlled error carrying its wire code as the exception message\.

```csharp
public ErrorResponseException(string error);
```
#### Parameters

<a id='DarkWS.ErrorResponseException.ErrorResponseException(string).error'></a>

`error` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.ErrorResponseException.ErrorResponseException(string,System.Exception)'></a>

## ErrorResponseException\(string, Exception\) Constructor

Creates a controlled error with its original cause\.

```csharp
public ErrorResponseException(string error, System.Exception? innerException);
```
#### Parameters

<a id='DarkWS.ErrorResponseException.ErrorResponseException(string,System.Exception).error'></a>

`error` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.ErrorResponseException.ErrorResponseException(string,System.Exception).innerException'></a>

`innerException` [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception')
### Methods

<a id='DarkWS.ErrorResponseException.GetResponse()'></a>

## ErrorResponseException\.GetResponse\(\) Method

Returns the controlled protocol response associated with this exception\.

```csharp
public override DarkWS.IResponse GetResponse();
```

#### Returns
[IResponse](DarkWS.IResponse.md 'DarkWS\.IResponse')