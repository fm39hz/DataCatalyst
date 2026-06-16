namespace FM39hz.DataCatalyst.Runtime;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public static class DataFileLoader {
    public static List<DataOverride> LoadOverrides(DataSource[] sources) {
        var overrides = new List<DataOverride>();
        var seen = new HashSet<string>();
        foreach (var source in sources) {
            if (!Directory.Exists(source.Directory)) continue;
            foreach (var file in Directory.GetFiles(source.Directory, "*.json", SearchOption.TopDirectoryOnly)) {
                try {
                    var text = File.ReadAllText(file);
                    var json = JsonDocument.Parse(text);
                    foreach (var prop in json.RootElement.EnumerateObject()) {
                        if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                        if (!seen.Add(source.Name + ":" + prop.Name)) continue;
                        var dataOverride = new DataOverride { Target = prop.Name };
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
                        overrides.Add(dataOverride);
                    }
                } catch { /* skip invalid */ }
            }
        }
        return overrides;
    }
}
