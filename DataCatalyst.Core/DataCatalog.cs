namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

public sealed class DataCatalog {
    private readonly Dictionary<string, DataEntry> _entries;

    internal DataCatalog(Dictionary<string, DataEntry> entries) {
        _entries = entries;
    }

    public IReadOnlyDictionary<string, DataEntry> Entries => _entries;

    public T Get<T>(string key) where T : struct {
        if (_entries.TryGetValue(key, out var entry))
            return entry.Get<T>();
        throw new KeyNotFoundException($"Entry '{key}' not found");
    }

    public bool TryGet<T>(string key, out T value) where T : struct {
        if (_entries.TryGetValue(key, out var entry) && entry.TryGet(out value))
            return true;
        value = default;
        return false;
    }

    public bool ContainsKey(string key) => _entries.ContainsKey(key);
}
