namespace FM39hz.DataCatalyst.Runtime;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public static class DataMerge {
    public static event Action<string, string, object?, object?>? OnConflict;

    public static void Load(DataSource[] sources) {
        var overrides = new List<DataOverride>();
        var existing = new Dictionary<string, Dictionary<string, object?>>();

        foreach (var source in sources) {
            if (!Directory.Exists(source.Directory)) continue;
            foreach (var file in Directory.GetFiles(source.Directory, "*.json", SearchOption.TopDirectoryOnly)) {
                CollectOverrides(file, overrides, existing);
            }
        }

        DataContextRegistry.InitializeAll(overrides);
    }

    private static void CollectOverrides(string path, List<DataOverride> overrides,
        Dictionary<string, Dictionary<string, object?>> existing) {
        try {
            var text = File.ReadAllText(path);
            var json = JsonDocument.Parse(text);
            foreach (var prop in json.RootElement.EnumerateObject()) {
                var dataOverride = new DataOverride { Target = prop.Name };
                if (prop.Value.ValueKind != JsonValueKind.Object) continue;

                if (!existing.TryGetValue(prop.Name, out var fields)) {
                    fields = new Dictionary<string, object?>();
                    existing[prop.Name] = fields;
                }

                foreach (var field in prop.Value.EnumerateObject()) {
                    object? val = field.Value.ValueKind switch {
                        JsonValueKind.Number when field.Value.TryGetInt32(out var i) => i,
                        JsonValueKind.Number when field.Value.TryGetSingle(out var f) => f,
                        JsonValueKind.True => (object?)true,
                        JsonValueKind.False => (object?)false,
                        JsonValueKind.String => field.Value.GetString(),
                        _ => null,
                    };
                    if (val is null) continue;

                    // Conflict detection
                    if (fields.TryGetValue(field.Name, out var prev) && !Equals(prev, val)) {
                        OnConflict?.Invoke(prop.Name, field.Name, prev, val);
                    }
                    fields[field.Name] = val;
                    dataOverride.Fields[field.Name] = val;
                }
                overrides.Add(dataOverride);
            }
        } catch { /* skip */ }
    }
}
