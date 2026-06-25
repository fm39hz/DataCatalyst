using System;
using System.Collections.Generic;

namespace DataCatalyst.Storage;

public static class AspectTypeRegistry
{
    static readonly Dictionary<string, Type> _typeByName = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<Type, Func<object, object?>> _deserializers = new();

    public static bool TryGetType(string name, out Type? type)
        => _typeByName.TryGetValue(name, out type);

    public static bool TryGetType(ReadOnlySpan<char> name, out Type? type)
        => _typeByName.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out type);

    public static bool HasType(string name) => _typeByName.ContainsKey(name);

    public static void Register(Type type)
        => _typeByName[type.Name] = type;

    public static void RegisterDeserializer(Type type, Func<object, object?> d)
        => _deserializers[type] = d;

    public static object? Deserialize(Type type, object? raw)
        => _deserializers.TryGetValue(type, out var d) ? d(raw!) : null;
}
