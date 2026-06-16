namespace FM39hz.DataCatalyst.Runtime;

using System;
using System.Collections.Generic;

public static class DataDslRegistry {
    private static readonly HashSet<Type> _dslTypes = new();
    private static readonly object _lock = new();

    public static void Register<T>() {
        lock (_lock) _dslTypes.Add(typeof(T));
    }

    public static bool IsRegistered<T>() {
        lock (_lock) return _dslTypes.Contains(typeof(T));
    }

    public static Type[] GetAll() {
        lock (_lock) {
            var result = new Type[_dslTypes.Count];
            _dslTypes.CopyTo(result);
            return result;
        }
    }
}
