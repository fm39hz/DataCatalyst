namespace FM39hz.DataCatalyst.Runtime;

using System;
using System.Collections.Generic;

public sealed class DataChangedArgs : EventArgs {
    public IReadOnlyList<DataOverride> Overrides { get; }
    public DataChangedArgs(IReadOnlyList<DataOverride> overrides) => Overrides = overrides;
}

public static class DataContextRegistry {
    private static readonly List<Action<IReadOnlyList<DataOverride>>> _initializers = new();
    private static readonly object _lock = new();

    public static event EventHandler<DataChangedArgs>? OnDataChanged;

    public static void Register(Action<IReadOnlyList<DataOverride>> initialize) {
        lock (_lock) _initializers.Add(initialize);
    }

    public static void InitializeAll(IReadOnlyList<DataOverride>? overrides = null) {
        var list = overrides ?? Array.Empty<DataOverride>();
        Action<IReadOnlyList<DataOverride>>[] snapshot;
        lock (_lock) snapshot = _initializers.ToArray();
        foreach (var init in snapshot) {
            init(list);
        }
        OnDataChanged?.Invoke(null, new DataChangedArgs(list));
    }

    public static void Reset() {
        lock (_lock) _initializers.Clear();
    }
}
