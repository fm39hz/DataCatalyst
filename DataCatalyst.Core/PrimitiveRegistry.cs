namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

public static class PrimitiveRegistry {
    private static readonly HashSet<Type> _types = new();

    public static void Register<T>() {
        lock (_types) _types.Add(typeof(T));
    }

    public static bool IsRegistered(Type type) {
        lock (_types) return _types.Contains(type);
    }

    public static IReadOnlyCollection<Type> GetAll() {
        lock (_types) return new List<Type>(_types);
    }

    public static void Clear() {
        lock (_types) _types.Clear();
    }
}
