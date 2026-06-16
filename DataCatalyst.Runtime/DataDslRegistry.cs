namespace DataCatalyst.Runtime;

using System;
using System.Collections.Generic;
using System.Text.Json;

public static class DataDslRegistry {
    private static readonly HashSet<Type> _dslTypes = new();
    private static readonly Dictionary<Type, JsonSerializerOptions> _options = new();
    private static readonly object _lock = new();

    public static void Register<T>(JsonSerializerOptions? options = null) {
        lock (_lock) {
            _dslTypes.Add(typeof(T));
            if (options is not null) _options[typeof(T)] = options;
        }
    }

    public static bool IsRegistered<T>() {
        lock (_lock) return _dslTypes.Contains(typeof(T));
    }

    public static T? Deserialize<T>(string json) where T : class {
        lock (_lock) {
            if (!_dslTypes.Contains(typeof(T)))
                throw new InvalidOperationException($"Type {typeof(T).Name} not registered");
            JsonSerializerOptions? opts = null;
            if (_options.TryGetValue(typeof(T), out var o)) opts = o;
            return JsonSerializer.Deserialize<T>(json, opts);
        }
    }

    public static object? Deserialize(string dslTypeName, string json) {
        lock (_lock) {
            foreach (var type in _dslTypes) {
                if (type.Name == dslTypeName || type.FullName == dslTypeName) {
                    JsonSerializerOptions? opts = null;
                    if (_options.TryGetValue(type, out var o)) opts = o;
                    return JsonSerializer.Deserialize(json, type, opts);
                }
            }
            throw new InvalidOperationException($"DSL type '{dslTypeName}' not registered");
        }
    }

    public static Type[] GetAll() {
        lock (_lock) {
            var result = new Type[_dslTypes.Count];
            _dslTypes.CopyTo(result);
            return result;
        }
    }
}
