namespace DarkWS;

[AttributeUsage(AttributeTargets.Class)]
public class HandlerAttribute(string name) : Attribute {
    public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Method)]
public class ActionAttribute(string name) : Attribute {
    public string Name { get; } = name;
}
