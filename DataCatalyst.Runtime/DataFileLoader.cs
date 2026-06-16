namespace DataCatalyst.Runtime;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public static class DataFileLoader {
    public static List<DataOverride> LoadFromDirectory(string directory) {
        var overrides = new List<DataOverride>();
        if (!Directory.Exists(directory)) return overrides;

        foreach (var file in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)) {
            try {
                var text = File.ReadAllText(file);
                var json = JsonDocument.Parse(text);
                foreach (var prop in json.RootElement.EnumerateObject()) {
                    if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                    overrides.Add(new DataOverride {
                        Target = prop.Name,
                        RawJson = prop.Value.GetRawText()
                    });
                }
            } catch { /* skip invalid */ }
        }
        return overrides;
    }
}
