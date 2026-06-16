namespace DataCatalyst.Runtime;

using System;
using System.Collections.Generic;
using System.Text.Json;

public sealed class DataChangedArgs : EventArgs {
    public IReadOnlyList<DataOverride> Overrides { get; }
    public DataChangedArgs(IReadOnlyList<DataOverride> overrides) => Overrides = overrides;
}

public static class DataContextRegistry {
    private static readonly Dictionary<string, Action<JsonElement?>> _handlers = new();
    private static readonly object _lock = new();

    public static event EventHandler<DataChangedArgs>? OnDataChanged;

    public static void Register<T>(Action<JsonElement?> bootstrap) where T : struct {
        lock (_lock) _handlers[typeof(T).Name] = bootstrap;
    }

    public static void InitializeAll(IReadOnlyList<DataOverride>? overrides = null) {
        Action<JsonElement?>[] snapshot;
        string[] names;

        lock (_lock) {
            snapshot = new Action<JsonElement?>[_handlers.Count];
            names = new string[_handlers.Count];
            _handlers.Keys.CopyTo(names, 0);
            _handlers.Values.CopyTo(snapshot, 0);
        }

        // Phase 1: init defaults (no override)
        foreach (var handler in snapshot) handler(null);

        // Phase 2: apply overrides
        if (overrides is not null) {
            foreach (var o in overrides) {
                if (o.RawJson is null) continue;
                if (_handlers.TryGetValue(o.Target, out var typedHandler)) {
                    try {
                        var doc = JsonDocument.Parse(o.RawJson);
                        typedHandler(doc.RootElement);
                        doc.Dispose();
                    } catch { /* skip invalid */ }
                }
            }
        }

        OnDataChanged?.Invoke(null, new DataChangedArgs(overrides ?? Array.Empty<DataOverride>()));
    }

    public static void Reset() {
        lock (_lock) _handlers.Clear();
    }
}
