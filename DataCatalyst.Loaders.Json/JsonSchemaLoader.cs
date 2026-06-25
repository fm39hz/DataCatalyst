#pragma warning disable RS1035
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DataCatalyst.Schema;

namespace DataCatalyst.Loaders;

public sealed class JsonSchemaLoader : ISchemaLoader
{
    static readonly Dictionary<string, Type> TypeMap = new(StringComparer.OrdinalIgnoreCase) {
        ["int"] = typeof(int), ["int32"] = typeof(int), ["long"] = typeof(long), ["int64"] = typeof(long),
        ["float"] = typeof(float), ["single"] = typeof(float), ["double"] = typeof(double),
        ["string"] = typeof(string), ["bool"] = typeof(bool), ["boolean"] = typeof(bool),
    };

    public SchemaRegistry LoadSchema(string content)
    {
        var reg = new SchemaRegistry();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        if (root.TryGetProperty("$aspects", out var ap) && ap.ValueKind == JsonValueKind.Object)
            foreach (var a in ap.EnumerateObject()) {
                var fields = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                if (a.Value.ValueKind == JsonValueKind.Object)
                    foreach (var f in a.Value.EnumerateObject())
                        fields[f.Name] = TypeMap.TryGetValue(f.Value.GetString() ?? "string", out var t) ? t : typeof(string);
                reg.DefineAspect(a.Name, fields);
            }

        if (root.TryGetProperty("$concepts", out var cp) && cp.ValueKind == JsonValueKind.Object)
            foreach (var c in cp.EnumerateObject()) reg.DefineConcept(c.Name, Array.Empty<string>());

        if (root.TryGetProperty("$mapping", out var mp) && mp.ValueKind == JsonValueKind.Object)
            foreach (var e in mp.EnumerateObject())
                if (e.Value.ValueKind == JsonValueKind.Array)
                    reg.DefineConcept(e.Name, e.Value.EnumerateArray().Select(x => x.GetString()!).Where(s => !string.IsNullOrEmpty(s)).ToArray());

        return reg;
    }

    public SchemaRegistry LoadSchemaFile(string path) => LoadSchema(File.ReadAllText(path));

    public SchemaRegistry LoadSchemaDirectory(string path)
    {
        var reg = new SchemaRegistry();
        if (!Directory.Exists(path)) return reg;
        foreach (var f in Directory.EnumerateFiles(path, "*.json")) reg.MergeFrom(LoadSchemaFile(f));
        return reg;
    }
}
