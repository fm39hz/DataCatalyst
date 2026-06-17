namespace DataCatalyst.Core;

using System.Collections.Generic;
using DataCatalyst.Abstractions;

public static class DataViewAdapterRegistry {
    private static readonly RegistryStore<System.Type, object> _adapters = new();

    public static void Register<T>(IDataViewAdapter<T> adapter) {
        var t = typeof(T);
        if (_adapters.TryGet(t, out var existing) && existing is List<IDataViewAdapter<T>> list) {
            list.Add(adapter);
        } else {
            _adapters.Add(t, new List<IDataViewAdapter<T>> { adapter });
        }
    }

    public static IEnumerable<IDataViewAdapter<T>> GetAdapters<T>() {
        if (_adapters.TryGet(typeof(T), out var existing) && existing is List<IDataViewAdapter<T>> list) {
            return list.ToArray();
        }
        return [];
    }
}
