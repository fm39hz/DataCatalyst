namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

public sealed class DataEntry {
    public string Key { get; }
    public List<string>? Inherits { get; internal set; }
    public IReadOnlyDictionary<Type, object> Components => _components;
    internal Dictionary<Type, object> _components;
    internal bool _resolved;

    public DataEntry(string key, Dictionary<Type, object>? components = null, List<string>? inherits = null) {
        Key = key;
        _components = components ?? new Dictionary<Type, object>();
        Inherits = inherits;
    }

    public void Set<T>(T value) where T : struct {
        _components[typeof(T)] = value;
    }

    public T Get<T>() where T : struct {
        if (_components.TryGetValue(typeof(T), out var boxed))
            return (T)boxed;
        throw new KeyNotFoundException($"Component '{typeof(T).Name}' not found in entry '{Key}'");
    }

    public bool TryGet<T>(out T value) where T : struct {
        if (_components.TryGetValue(typeof(T), out var boxed)) {
            value = (T)boxed;
            return true;
        }
        value = default;
        return false;
    }

    public bool Has<T>() where T : struct => _components.ContainsKey(typeof(T));
}
