using System;
using System.Collections.Generic;

namespace DataCatalyst.Storage;

public static class AspectTypeRegistry
{
    private static readonly Dictionary<string, Type> _typeByName
        = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Type, Func<object, object?>> _deserializers
        = new();

    public static bool TryGetType(string name, [global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Type? type)
        => _typeByName.TryGetValue(name, out type);

    public static void Register(Type type)
        => _typeByName[type.Name] = type;

    public static void RegisterDeserializer(Type type, Func<object, object?> deserializer)
        => _deserializers[type] = deserializer;

    public static object? Deserialize(Type type, object? rawVal)
        => _deserializers.TryGetValue(type, out var d) ? d(rawVal!) : null;
}
