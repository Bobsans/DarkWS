#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## ActionAttribute Class

Exposes a declared public instance method returning IResponse or Task of IResponse with zero or one payload parameter\.

```csharp
public class ActionAttribute : System.Attribute
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → ActionAttribute
### Constructors

<a id='DarkWS.ActionAttribute.ActionAttribute(string)'></a>

## ActionAttribute\(string\) Constructor

Exposes a declared public instance method returning IResponse or Task of IResponse with zero or one payload parameter\.

```csharp
public ActionAttribute(string name);
```
#### Parameters

<a id='DarkWS.ActionAttribute.ActionAttribute(string).name'></a>

`name` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Wire name or prefix\.
### Properties

<a id='DarkWS.ActionAttribute.Name'></a>

## ActionAttribute\.Name Property

Gets the name used in the wire action key\.

```csharp
public string Name { get; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')