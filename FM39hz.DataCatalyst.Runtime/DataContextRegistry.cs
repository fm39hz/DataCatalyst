namespace FM39hz.DataCatalyst.Runtime;

using System;
using System.Collections.Generic;

public static class DataContextRegistry {
    private static readonly List<Action<IReadOnlyList<DataOverride>>> _initializers = new();
    private static readonly object _lock = new();

    public static void Register(Action<IReadOnlyList<DataOverride>> initialize) {
        lock (_lock) _initializers.Add(initialize);
    }

    public static void InitializeAll(IReadOnlyList<DataOverride>? overrides = null) {
        Action<IReadOnlyList<DataOverride>>[] snapshot;
        lock (_lock) snapshot = _initializers.ToArray();
        foreach (var init in snapshot) {
            init(overrides ?? Array.Empty<DataOverride>());
        }
    }

    public static void Reset() {
        lock (_lock) _initializers.Clear();
    }
}
