namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

internal sealed class RegistryStore<TKey, TValue> where TKey : notnull {
    private readonly Dictionary<TKey, TValue> _items = new();
    private readonly object _lock = new();

    public void Add(TKey key, TValue value) {
        lock (_lock) _items[key] = value;
    }

    public bool TryGet(TKey key, out TValue? value) {
        lock (_lock) return _items.TryGetValue(key, out value);
    }

    public TValue[] GetAll() {
        lock (_lock) {
            var result = new TValue[_items.Count];
            _items.Values.CopyTo(result, 0);
            return result;
        }
    }

    public void Remove(TKey key) {
        lock (_lock) _items.Remove(key);
    }

    public void Clear() {
        lock (_lock) _items.Clear();
    }
}
