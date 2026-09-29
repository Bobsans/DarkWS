#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## ErrorMessage\<T\> Class

Error response carrying a stable code and optional typed details\.

```csharp
public sealed record ErrorMessage<T> : System.IEquatable<DarkWS.ErrorMessage<T>>
```
#### Type parameters

<a id='DarkWS.ErrorMessage_T_.T'></a>

`T`

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → ErrorMessage\<T\>

Implements [System\.IEquatable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')[DarkWS\.ErrorMessage&lt;](DarkWS.ErrorMessage_T_.md 'DarkWS\.ErrorMessage\<T\>')[T](DarkWS.ErrorMessage_T_.md#DarkWS.ErrorMessage_T_.T 'DarkWS\.ErrorMessage\<T\>\.T')[&gt;](DarkWS.ErrorMessage_T_.md 'DarkWS\.ErrorMessage\<T\>')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1 'System\.IEquatable\`1')
### Constructors

<a id='DarkWS.ErrorMessage_T_.ErrorMessage(string,string,T)'></a>

## ErrorMessage\(string, string, T\) Constructor

Error response carrying a stable code and optional typed details\.

```csharp
public ErrorMessage(string Id, string Error, T? Data);
```
#### Parameters

<a id='DarkWS.ErrorMessage_T_.ErrorMessage(string,string,T).Id'></a>

`Id` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Correlation or identity key\.

<a id='DarkWS.ErrorMessage_T_.ErrorMessage(string,string,T).Error'></a>

`Error` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Stable error code\.

<a id='DarkWS.ErrorMessage_T_.ErrorMessage(string,string,T).Data'></a>

`Data` [T](DarkWS.ErrorMessage_T_.md#DarkWS.ErrorMessage_T_.T 'DarkWS\.ErrorMessage\<T\>\.T')

Optional result or notification data\.
### Properties

<a id='DarkWS.ErrorMessage_T_.Data'></a>

## ErrorMessage\<T\>\.Data Property

Optional result or notification data\.

```csharp
public T? Data { get; init; }
```

#### Property Value
[T](DarkWS.ErrorMessage_T_.md#DarkWS.ErrorMessage_T_.T 'DarkWS\.ErrorMessage\<T\>\.T')

<a id='DarkWS.ErrorMessage_T_.Error'></a>

## ErrorMessage\<T\>\.Error Property

Stable error code\.

```csharp
public string Error { get; init; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.ErrorMessage_T_.Id'></a>

## ErrorMessage\<T\>\.Id Property

Correlation or identity key\.

```csharp
public string Id { get; init; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')