using System.Reflection;

namespace DarkWS;

/// <summary>Metadata for a registered action, shared by its message scope and filters.</summary>
public sealed class DarkWsActionInfo {
    internal DarkWsActionInfo(string name, Type handlerType, MethodInfo method, IReadOnlyList<Attribute> handlerAttributes) {
        Name = name;
        HandlerType = handlerType;
        Method = method;
        Attributes = Array.AsReadOnly(method.GetCustomAttributes(inherit: true).OfType<Attribute>().ToArray());
        HandlerAttributes = handlerAttributes;
    }

    /// <summary>Gets the registered action name, including the handler prefix when present.</summary>
    public string Name { get; }

    /// <summary>Gets the concrete handler type.</summary>
    public Type HandlerType { get; }

    /// <summary>Gets the action method.</summary>
    public MethodInfo Method { get; }

    /// <summary>Gets attributes on the action method, including inherited attributes.</summary>
    public IReadOnlyList<Attribute> Attributes { get; }

    /// <summary>Gets attributes on the handler class, including inherited attributes, collected once at registration.</summary>
    public IReadOnlyList<Attribute> HandlerAttributes { get; }

    /// <summary>Returns whether the action method or its handler class carries an attribute of this type, including inherited and derived attributes.</summary>
    public bool HasAttribute<T>() where T : Attribute {
        return Attributes.OfType<T>().Any() || HandlerAttributes.OfType<T>().Any();
    }
}
