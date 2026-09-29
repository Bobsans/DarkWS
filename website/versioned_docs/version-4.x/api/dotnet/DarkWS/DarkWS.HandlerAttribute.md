#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## HandlerAttribute Class

Defines the wire action prefix for a handler class\.

```csharp
public class HandlerAttribute : System.Attribute
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → HandlerAttribute
### Constructors

<a id='DarkWS.HandlerAttribute.HandlerAttribute(string)'></a>

## HandlerAttribute\(string\) Constructor

Defines the wire action prefix for a handler class\.

```csharp
public HandlerAttribute(string name);
```
#### Parameters

<a id='DarkWS.HandlerAttribute.HandlerAttribute(string).name'></a>

`name` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

Wire name or prefix\.
### Properties

<a id='DarkWS.HandlerAttribute.Name'></a>

## HandlerAttribute\.Name Property

Gets the name used in the wire action key\.

```csharp
public string Name { get; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')