#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## DarkWsException Class

Base for controlled handler errors that supply their own protocol response\.

```csharp
public abstract class DarkWsException : System.Exception
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') → DarkWsException

Derived  
↳ [ErrorResponseException](DarkWS.ErrorResponseException.md 'DarkWS\.ErrorResponseException')  
↳ [ErrorResponseException&lt;T&gt;](DarkWS.ErrorResponseException_T_.md 'DarkWS\.ErrorResponseException\<T\>')
### Constructors

<a id='DarkWS.DarkWsException.DarkWsException()'></a>

## DarkWsException\(\) Constructor

Creates a controlled exception with the default exception message\.

```csharp
protected DarkWsException();
```

<a id='DarkWS.DarkWsException.DarkWsException(string,System.Exception)'></a>

## DarkWsException\(string, Exception\) Constructor

Creates a controlled exception carrying a non\-empty error code and optional cause\.

```csharp
protected DarkWsException(string error, System.Exception? innerException=null);
```
#### Parameters

<a id='DarkWS.DarkWsException.DarkWsException(string,System.Exception).error'></a>

`error` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.DarkWsException.DarkWsException(string,System.Exception).innerException'></a>

`innerException` [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception')
### Methods

<a id='DarkWS.DarkWsException.GetResponse()'></a>

## DarkWsException\.GetResponse\(\) Method

Returns the controlled protocol response associated with this exception\.

```csharp
public abstract DarkWS.IResponse GetResponse();
```

#### Returns
[IResponse](DarkWS.IResponse.md 'DarkWS\.IResponse')