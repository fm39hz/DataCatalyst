namespace DataCatalyst.Loaders;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DataCatalyst.Core;

public static class JsonDataLoader {
    public static List<DataEntry> LoadDirectory(string directory) {
        var entries = new List<DataEntry>();
        if (!Directory.Exists(directory)) return entries;

        var knownPrimitives = PrimitiveRegistry.GetAll();

        foreach (var file in Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories)) {
            try {
                var text = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;

                var key = MakeKey(directory, file);

                List<string>? inherits = null;
                if (root.TryGetProperty("inherits", out var inhEl) && inhEl.ValueKind == JsonValueKind.Array) {
                    inherits = new List<string>();
                    foreach (var item in inhEl.EnumerateArray())
                        inherits.Add(item.GetString() ?? "");
                }

                var components = new Dictionary<Type, object>();
                foreach (var prop in root.EnumerateObject()) {
                    if (prop.Name == "inherits") continue;
                    if (prop.Value.ValueKind != JsonValueKind.Object) continue;

                    var type = FindType(prop.Name, knownPrimitives);
                    if (type == null) continue;

                    try {
                        var deserialized = JsonSerializer.Deserialize(prop.Value.GetRawText(), type);
                        if (deserialized != null)
                            components[type] = deserialized;
                    } catch { /* skip invalid component */ }
                }

                entries.Add(new DataEntry(key, components, inherits));
            } catch { /* skip invalid file */ }
        }

        return entries;
    }

    private static string MakeKey(string rootDir, string filePath) {
        var fullDir = Path.GetFullPath(rootDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var rel = filePath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase)
            ? filePath.Substring(fullDir.Length) : filePath;
        return Path.ChangeExtension(rel, null).Replace('\\', '.').Replace('/', '.');
    }

    private static Type? FindType(string name, IReadOnlyCollection<Type> known) {
        foreach (var t in known)
            if (t.Name == name) return t;
        return null;
    }
}
