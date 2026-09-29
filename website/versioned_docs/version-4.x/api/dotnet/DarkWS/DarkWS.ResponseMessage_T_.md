#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## ResponseMessage\<T\> Class

Successful response carrying correlated result data\.

```csharp
public sealed record ResponseMessage<T> : System.IEquatable<DarkWS.ResponseMessage<T>>
```
#### Type parameters

<a id='DarkWS.ResponseMessage_T_.T'></a>

`T`

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → ResponseMessage\<T\>

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[DarkWS\.ResponseMessage&lt;](DarkWS.ResponseMessage_T_.md 'DarkWS\.ResponseMessage\<T\>')[T](DarkWS.ResponseMessage_T_.md#DarkWS.ResponseMessage_T_.T 'DarkWS\.ResponseMessage\<T\>\.T')[&gt;](DarkWS.ResponseMessage_T_.md 'DarkWS\.ResponseMessage\<T\>')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')
### Constructors

<a id='DarkWS.ResponseMessage_T_.ResponseMessage(string,T)'></a>

## ResponseMessage\(string, T\) Constructor

Successful response carrying correlated result data\.

```csharp
public ResponseMessage(string Id, T? Data);
```
#### Parameters

<a id='DarkWS.ResponseMessage_T_.ResponseMessage(string,T).Id'></a>

`Id` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Correlation or identity key\.

<a id='DarkWS.ResponseMessage_T_.ResponseMessage(string,T).Data'></a>

`Data` [T](DarkWS.ResponseMessage_T_.md#DarkWS.ResponseMessage_T_.T 'DarkWS\.ResponseMessage\<T\>\.T')

Optional result or notification data\.
### Properties

<a id='DarkWS.ResponseMessage_T_.Data'></a>

## ResponseMessage\<T\>\.Data Property

Optional result or notification data\.

```csharp
public T? Data { get; init; }
```

#### Property Value
[T](DarkWS.ResponseMessage_T_.md#DarkWS.ResponseMessage_T_.T 'DarkWS\.ResponseMessage\<T\>\.T')

<a id='DarkWS.ResponseMessage_T_.Id'></a>

## ResponseMessage\<T\>\.Id Property

Correlation or identity key\.

```csharp
public string Id { get; init; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')