using System.Collections.Concurrent;
using System.Reflection;

namespace HmiLink.Transport;

// Optional stable wire identifier. Without it, a shared DTO's full C# type name is used.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class HmiLinkMessageAttribute : Attribute
{
    public string Name { get; }

    public HmiLinkMessageAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Message name cannot be empty", nameof(name));
        Name = name;
    }
}

public static class HmiLinkMessageNames
{
    private static readonly ConcurrentDictionary<Type, string> Cache = new();

    public static string For<TMessage>() => For(typeof(TMessage));

    public static string For(Type messageType) => Cache.GetOrAdd(messageType, type =>
        type.GetCustomAttribute<HmiLinkMessageAttribute>()?.Name
        ?? type.FullName
        ?? throw new InvalidOperationException($"Type {type} has no stable message name"));
}
