namespace FM39hz.DataCatalyst.Plugins.Modding.Runtime;

using System;
using System.Collections.Generic;
public sealed class ModServiceRegistry : IModServiceRegistry {
    private readonly Dictionary<Type, (string? Owner, object Instance)> _services = new();
    private readonly object _lock = new();

    public IReadOnlyList<Type> AllServices {
        get {
            lock (_lock) return new List<Type>(_services.Keys);
        }
    }

    public void RegisterGlobal<T>(T service) where T : class {
        lock (_lock) _services[typeof(T)] = (null, service);
    }

    public T? GetGlobal<T>() where T : class {
        lock (_lock) {
            if (_services.TryGetValue(typeof(T), out var entry) && entry.Owner is null)
                return (T)entry.Instance;
            return null;
        }
    }

    public void RegisterScoped<T>(string ownerModId, T service) where T : class {
        lock (_lock) _services[typeof(T)] = (ownerModId, service);
    }

    public T? GetScoped<T>(string ownerModId) where T : class {
        lock (_lock) {
            if (_services.TryGetValue(typeof(T), out var entry) && entry.Owner == ownerModId)
                return (T)entry.Instance;
            return null;
        }
    }

    public T? GetScopedAny<T>(IReadOnlyList<string> candidateOwners) where T : class {
        lock (_lock) {
            for (var i = 0; i < candidateOwners.Count; i++) {
                if (_services.TryGetValue(typeof(T), out var entry) && entry.Owner == candidateOwners[i])
                    return (T)entry.Instance;
            }
            return null;
        }
    }
}
