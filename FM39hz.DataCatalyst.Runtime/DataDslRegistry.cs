namespace FM39hz.DataCatalyst.Runtime;

using System;
using System.Collections.Generic;

public static class DataDslRegistry {
    private static readonly Dictionary<string, Type> _dsls = new();
    private static readonly object _lock = new();

    public static void Register(string dslId, Type type) {
        lock (_lock) _dsls[dslId] = type;
    }

    public static Type? GetType(string dslId) {
        lock (_lock) return _dsls.TryGetValue(dslId, out var t) ? t : null;
    }

    public static bool IsRegistered(string dslId) {
        lock (_lock) return _dsls.ContainsKey(dslId);
    }
}
