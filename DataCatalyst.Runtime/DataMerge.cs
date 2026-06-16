namespace DataCatalyst.Runtime;

using System;
using System.Collections.Generic;

public sealed class DataChangeRecord {
    public string Target { get; }
    public string Field { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }
    public string SourceName { get; }

    public DataChangeRecord(string target, string field, object? old, object? @new, string source) {
        Target = target; Field = field; OldValue = old; NewValue = @new; SourceName = source;
    }
}

public static class DataMerge {
    public static event Action<string, string, object?, object?>? OnConflict;
    public static event Action<DataChangeRecord>? OnDataChanged;

    public static void Load(DataSource[] sources) {
        var overrides = DataFileLoader.LoadOverrides(sources);
        Apply(overrides);
    }

    public static void Apply(List<DataOverride> overrides) {
        var existing = new Dictionary<string, Dictionary<string, object?>>();
        foreach (var dataOverride in overrides) {
            if (!existing.TryGetValue(dataOverride.Target, out var fields)) {
                fields = new Dictionary<string, object?>();
                existing[dataOverride.Target] = fields;
            }
            foreach (var kv in dataOverride.Fields) {
                fields.TryGetValue(kv.Key, out var prev);
                if (!Equals(prev, kv.Value)) {
                    OnConflict?.Invoke(dataOverride.Target, kv.Key, prev, kv.Value);
                    OnDataChanged?.Invoke(new DataChangeRecord(dataOverride.Target, kv.Key, prev, kv.Value, "merge"));
                }
                fields[kv.Key] = kv.Value;
            }
        }
        DataContextRegistry.InitializeAll(overrides);
    }
}
