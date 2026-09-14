namespace DarkWS;

/// <summary>Defines the wire action prefix for a handler class.</summary>
/// <param name="name">Wire name or prefix.</param>
[AttributeUsage(AttributeTargets.Class)]
public class HandlerAttribute(string name) : Attribute {
    /// <summary>Gets the name used in the wire action key.</summary>
    public string Name { get; } = name;
}

/// <summary>Exposes a declared public instance method returning IResponse or Task of IResponse with zero or one payload parameter.</summary>
/// <param name="name">Wire name or prefix.</param>
[AttributeUsage(AttributeTargets.Method)]
public class ActionAttribute(string name) : Attribute {
    /// <summary>Gets the name used in the wire action key.</summary>
    public string Name { get; } = name;
}
