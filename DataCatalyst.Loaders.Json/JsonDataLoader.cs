#pragma warning disable RS1035
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LoaderAbstractions = DataCatalyst.Loader;
using DataCatalyst.Storage;

namespace DataCatalyst.Loaders;

public sealed class JsonDataLoader : LoaderAbstractions.IDataLoader
{
    public LoaderAbstractions.LoadResult Load(string content, string fallbackKey)
    {
        var result = new LoaderAbstractions.LoadResult();
        try { ParseJson(content, fallbackKey, result); }
        catch (Exception ex) { result._diagnostics.Add($"Error parsing JSON for '{fallbackKey}': {ex.Message}"); }
        return result;
    }

    public LoaderAbstractions.LoadResult LoadFile(string path)
    {
        try { return Load(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path)); }
        catch (Exception ex)
        { var r = new LoaderAbstractions.LoadResult(); r._diagnostics.Add($"Error loading '{path}': {ex.Message}"); return r; }
    }

    public LoaderAbstractions.LoadResult LoadDirectory(string path)
    {
        var result = new LoaderAbstractions.LoadResult();
        if (!Directory.Exists(path)) { result._diagnostics.Add($"Directory not found: {path}"); return result; }
        foreach (var file in Directory.EnumerateFiles(path, "*.json"))
        { var fr = LoadFile(file); result._entries.AddRange(fr._entries); result._diagnostics.AddRange(fr._diagnostics); }
        return result;
    }

    void ParseJson(string json, string fallbackKey, LoaderAbstractions.LoadResult result)
    { using var doc = JsonDocument.Parse(json); WalkAndDiscover(doc.RootElement, null, fallbackKey, result); }

    void WalkAndDiscover(JsonElement el, string? parentKey, string? fn, LoaderAbstractions.LoadResult result, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>();
        if (el.ValueKind == JsonValueKind.Object) {
            if (TryExtractEntry(el, parentKey, fn, result, visited, out var _)) {
                foreach (var p in el.EnumerateObject())
                    if (p.Name != "$concepts" && p.Name != "$key" && p.Name != "$inherits")
                        WalkAndDiscover(p.Value, p.Name, fn, result, visited);
            } else { foreach (var p in el.EnumerateObject()) WalkAndDiscover(p.Value, p.Name, fn, result, visited); }
        } else if (el.ValueKind == JsonValueKind.Array) {
            foreach (var i in el.EnumerateArray()) WalkAndDiscover(i, null, fn, result, visited);
        }
    }

    bool TryExtractEntry(JsonElement obj, string? parentKey, string? fn, LoaderAbstractions.LoadResult result,
        HashSet<string> visited, out bool extracted)
    {
        extracted = false;
        if (!TryGetConcepts(obj, out var concepts) || concepts.Count == 0) return false;
        var key = ExtractKey(obj, parentKey, fn);
        if (key == null || !visited.Add(key)) {
            result._diagnostics.Add(key == null ? "Entry has no key" : $"Duplicate entry key '{key}' skipped");
            return true;
        }
        var entry = new RawEntry { Key = key };
        foreach (var c in concepts) { if (!string.IsNullOrEmpty(c)) { entry.Concepts.Add(c); entry.ConceptSet.Add(c); } }
        if (obj.TryGetProperty("$inherits", out var inh) && inh.ValueKind == JsonValueKind.String)
            entry.Inherits = inh.GetString();

        if (obj.TryGetProperty("$aspects", out var ap) && ap.ValueKind == JsonValueKind.Array) {
            foreach (var item in ap.EnumerateArray()) {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("$aspect", out var anp)) continue;
                var an = anp.GetString() ?? ""; if (string.IsNullOrEmpty(an)) continue;
                if (item.TryGetProperty("$ref", out var rp))
                    entry.RawFields[an] = rp.GetString();
                else {
                    var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var f in item.EnumerateObject()) { if (f.Name == "$aspect" || f.Name == "$ref") continue; d[f.Name] = ToObject(f.Value); }
                    entry.RawFields[an] = d;
                }
                entry.FieldNames.Add(an);
            }
        } else {
            foreach (var p in obj.EnumerateObject()) {
                if (p.Name == "$concepts" || p.Name == "$key" || p.Name == "$inherits") continue;
                entry.RawFields[p.Name] = ToObject(p.Value);
                entry.FieldNames.Add(p.Name);
            }
        }
        result._entries.Add(entry);
        extracted = true;
        return true;
    }

    static bool TryGetConcepts(JsonElement obj, out List<string> concepts)
    {
        concepts = new List<string>();
        if (obj.TryGetProperty("$concepts", out var p)) {
            if (p.ValueKind == JsonValueKind.String) concepts.Add(p.GetString()!);
            else if (p.ValueKind == JsonValueKind.Array)
                foreach (var i in p.EnumerateArray())
                    if (i.ValueKind == JsonValueKind.String) concepts.Add(i.GetString()!);
        }
        return concepts.Count > 0;
    }

    static string? ExtractKey(JsonElement obj, string? parentKey, string? fn)
    {
        if (!string.IsNullOrEmpty(parentKey)) return parentKey;
        if (obj.TryGetProperty("$key", out var p) && p.ValueKind == JsonValueKind.String) return p.GetString();
        return fn;
    }

    static object? ToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => ToObject(p.Value), StringComparer.OrdinalIgnoreCase),
        JsonValueKind.Array => el.EnumerateArray().Select(ToObject).ToList(),
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt32(out var i) ? i : el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };
}
