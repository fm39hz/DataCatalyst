#pragma warning disable RS1035
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DataCatalyst.Schema;
using DataCatalyst.Storage;
using LoaderAbstractions = DataCatalyst.Loader;

namespace DataCatalyst.Loaders;

public sealed class JsonSchemaLoader : ISchemaLoader
{
    /// <summary>Parse JSON token → CLR type mapping.</summary>
    private static readonly Dictionary<string, Type> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["int"] = typeof(int), ["int32"] = typeof(int),
        ["long"] = typeof(long), ["int64"] = typeof(long),
        ["float"] = typeof(float), ["single"] = typeof(float),
        ["double"] = typeof(double),
        ["string"] = typeof(string),
        ["bool"] = typeof(bool), ["boolean"] = typeof(bool),
    };

    public SchemaRegistry LoadSchema(string content)
    {
        var registry = new SchemaRegistry();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // Parse $aspects
        if (root.TryGetProperty("$aspects", out var aspectsProp) && aspectsProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var aspect in aspectsProp.EnumerateObject())
            {
                var fields = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                if (aspect.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in aspect.Value.EnumerateObject())
                    {
                        var typeStr = field.Value.GetString() ?? "string";
                        if (TypeMap.TryGetValue(typeStr, out var t))
                            fields[field.Name] = t;
                        else
                            fields[field.Name] = typeof(string);
                    }
                }
                registry.DefineAspect(aspect.Name, fields);
            }
        }

        // Parse $concepts
        if (root.TryGetProperty("$concepts", out var conceptsProp) && conceptsProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var concept in conceptsProp.EnumerateObject())
                registry.DefineConcept(concept.Name, Array.Empty<string>());
        }

        // Parse $mapping
        if (root.TryGetProperty("$mapping", out var mappingProp) && mappingProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in mappingProp.EnumerateObject())
            {
                if (entry.Value.ValueKind == JsonValueKind.Array)
                {
                    var aspects = entry.Value.EnumerateArray()
                        .Select(e => e.GetString()!)
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToArray();
                    registry.DefineConcept(entry.Name, aspects);
                }
            }
        }

        registry.Freeze();
        return registry;
    }

    public SchemaRegistry LoadSchemaFile(string path)
        => LoadSchema(File.ReadAllText(path));

    public SchemaRegistry LoadSchemaDirectory(string path)
    {
        var registry = new SchemaRegistry();
        if (!Directory.Exists(path)) return registry;
        foreach (var file in Directory.EnumerateFiles(path, "*.json"))
        {
            var r = LoadSchemaFile(file);
            // Merge: copy aspects + concepts from each file
            foreach (var a in r.Aspects)
                if (!registry.HasAspect(a.Key)) registry.DefineAspect(a.Key, new Dictionary<string, Type>(a.Value.Fields.ToDictionary(kv => kv.Key, kv => kv.Value)));
            foreach (var ca in r.ConceptAspects)
                registry.DefineConcept(ca.Key, ca.Value.ToArray());
        }
        registry.Freeze();
        return registry;
    }

    public LoaderAbstractions.LoadResult LoadEntries(string content, string key, SchemaRegistry schema)
    {
        var result = new LoaderAbstractions.LoadResult();
        try
        {
            using var doc = JsonDocument.Parse(content);
            WalkEntries(doc.RootElement, key, result, schema, new HashSet<string>());
        }
        catch (Exception ex)
        {
            result._diagnostics.Add($"Error: {ex.Message}");
        }
        return result;
    }

    private void WalkEntries(JsonElement el, string key, LoaderAbstractions.LoadResult result,
        SchemaRegistry schema, HashSet<string> visited)
    {
        if (el.ValueKind != JsonValueKind.Object) return;

        // Check if this object is an entry (has $concepts)
        if (el.TryGetProperty("$concepts", out var conceptsProp))
        {
            TryExtractEntry(el, key, result, schema, visited);
            return;
        }

        // Nested: walk children
        foreach (var prop in el.EnumerateObject())
            WalkEntries(prop.Value, prop.Name, result, schema, visited);
    }

    private void TryExtractEntry(JsonElement obj, string key, LoaderAbstractions.LoadResult result,
        SchemaRegistry schema, HashSet<string> visited)
    {
        if (!visited.Add(key))
        { result._diagnostics.Add($"Duplicate key '{key}'"); return; }

        // Parse $concepts
        var concepts = new List<string>();
        if (obj.TryGetProperty("$concepts", out var cp))
        {
            foreach (var c in cp.EnumerateArray())
            {
                var name = c.GetString() ?? "";
                if (!string.IsNullOrEmpty(name)) concepts.Add(name);
            }
        }

        // Parse $aspects array or fallback to raw fields
        var entry = new RawEntry { Key = key };
        foreach (var c in concepts)
        {
            entry.Concepts.Add(c);
            entry.ConceptSet.Add(c);
        }

        bool hasExplicitAspects = obj.TryGetProperty("$aspects", out var aspectsProp)
            && aspectsProp.ValueKind == JsonValueKind.Array;

        if (hasExplicitAspects)
        {
            // New format: $aspects array of objects with $aspect key
            foreach (var item in aspectsProp.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("$aspect", out var anameProp)) continue;
                var aname = anameProp.GetString() ?? "";
                if (string.IsNullOrEmpty(aname)) continue;

                // Validate aspect exists in schema
                if (!schema.HasAspect(aname))
                { result._diagnostics.Add($"Unknown aspect '{aname}' in entry '{key}'"); continue; }

                // Build raw field value
                var fieldDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in item.EnumerateObject())
                {
                    if (f.Name == "$aspect" || f.Name == "$ref") continue;
                    fieldDict[f.Name] = ToObject(f.Value);
                }

                // Handle $ref at aspect level
                if (item.TryGetProperty("$ref", out var refProp))
                    entry.RawFields[aname] = refProp.GetString();
                else
                    entry.RawFields[aname] = fieldDict;

                entry.FieldNames.Add(aname);
            }
        }
        else
        {
            // Fallback: old format — all non-special fields are aspects
            foreach (var prop in obj.EnumerateObject())
            {
                var name = prop.Name;
                if (name == "$concepts" || name == "$key" || name == "$inherits") continue;
                entry.RawFields[name] = ToObject(prop.Value);
                entry.FieldNames.Add(name);
            }
        }

        // Parse $inherits
        if (obj.TryGetProperty("$inherits", out var inh) && inh.ValueKind == JsonValueKind.String)
            entry.Inherits = inh.GetString();

        result._entries.Add(entry);
    }

    private static object? ToObject(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in el.EnumerateObject())
                {
                    if (p.Name == "$ref" && p.Value.ValueKind == JsonValueKind.String)
                        return p.Value.GetString(); // inline $ref
                    dict[p.Name] = ToObject(p.Value);
                }
                return dict;
            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var i in el.EnumerateArray()) list.Add(ToObject(i));
                return list;
            case JsonValueKind.String: return el.GetString();
            case JsonValueKind.Number:
                if (el.TryGetInt32(out var ival)) return ival;
                if (el.TryGetInt64(out var lval)) return lval;
                return el.GetDouble();
            case JsonValueKind.True: return true;
            case JsonValueKind.False: return false;
            default: return null;
        }
    }
}
