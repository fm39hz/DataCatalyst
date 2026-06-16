namespace FM39hz.DataCatalyst.Runtime;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FM39hz.DataCatalyst.Runtime;

public static class DataMerge {
    public static void Load(DataSource[] sources) {
        var overrides = new List<DataOverride>();

        // Priority order: later sources override earlier ones
        foreach (var source in sources) {
            if (!Directory.Exists(source.Directory)) continue;
            foreach (var file in Directory.GetFiles(source.Directory, "*.json", SearchOption.TopDirectoryOnly)) {
                CollectOverrides(file, overrides);
            }
        }

        DataContextRegistry.InitializeAll(overrides);
    }

    private static void CollectOverrides(string path, List<DataOverride> overrides) {
        try {
            var text = File.ReadAllText(path);
            var json = JsonDocument.Parse(text);
            foreach (var prop in json.RootElement.EnumerateObject()) {
                var dataOverride = new DataOverride { Target = prop.Name };
                if (prop.Value.ValueKind == JsonValueKind.Object) {
                    foreach (var field in prop.Value.EnumerateObject()) {
                        object? val = field.Value.ValueKind switch {
                            JsonValueKind.Number when field.Value.TryGetInt32(out var i) => i,
                            JsonValueKind.Number when field.Value.TryGetSingle(out var f) => f,
                            JsonValueKind.True => (object?)true,
                            JsonValueKind.False => (object?)false,
                            JsonValueKind.String => field.Value.GetString(),
                            _ => null,
                        };
                        if (val is not null) dataOverride.Fields[field.Name] = val;
                    }
                }
                overrides.Add(dataOverride);
            }
        } catch { /* skip invalid */ }
    }
}
