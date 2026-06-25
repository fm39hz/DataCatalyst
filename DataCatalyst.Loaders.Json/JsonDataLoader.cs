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
        try
        {
            ParseJson(content, fallbackKey, result);
        }
        catch (Exception ex)
        {
            result._diagnostics.Add($"Error parsing JSON for '{fallbackKey}': {ex.Message}");
        }
        return result;
    }

    public LoaderAbstractions.LoadResult LoadFile(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            var key = Path.GetFileNameWithoutExtension(path);
            return Load(text, key);
        }
        catch (Exception ex)
        {
            var result = new LoaderAbstractions.LoadResult();
            result._diagnostics.Add($"Error loading '{path}': {ex.Message}");
            return result;
        }
    }

    public LoaderAbstractions.LoadResult LoadDirectory(string path)
    {
        var result = new LoaderAbstractions.LoadResult();
        if (!Directory.Exists(path))
        {
            result._diagnostics.Add($"Directory not found: {path}");
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*.json"))
        {
            var fileResult = LoadFile(file);
            result._entries.AddRange(fileResult._entries);
            result._diagnostics.AddRange(fileResult._diagnostics);
        }

        return result;
    }

    private void ParseJson(string json, string fallbackKey, LoaderAbstractions.LoadResult result)
    {
        using var doc = JsonDocument.Parse(json);
        WalkAndDiscover(doc.RootElement, null, fallbackKey, result);
    }

    private void WalkAndDiscover(JsonElement element, string? parentKey, string? filename,
        LoaderAbstractions.LoadResult result, HashSet<string>? visitedKeys = null)
    {
        visitedKeys ??= new HashSet<string>();

        if (element.ValueKind == JsonValueKind.Object)
        {
            if (TryExtractEntry(element, parentKey, filename, result, visitedKeys, out var extracted))
            {
                // Entry found — continue walking for nested entries in remaining properties
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name == "$concepts" || prop.Name == "$key" || prop.Name == "$inherits")
                        continue;
                    WalkAndDiscover(prop.Value, prop.Name, filename, result, visitedKeys);
                }
            }
            else
            {
                foreach (var prop in element.EnumerateObject())
                    WalkAndDiscover(prop.Value, prop.Name, filename, result, visitedKeys);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                WalkAndDiscover(item, null, filename, result, visitedKeys);
        }
    }

    private bool TryExtractEntry(JsonElement obj, string? parentKey, string? filename,
        LoaderAbstractions.LoadResult result, HashSet<string> visitedKeys,
        out bool extracted)
    {
        extracted = false;

        if (!TryGetConcept(obj, out var concepts) || concepts.Count == 0)
            return false;

        var key = ExtractKey(obj, parentKey, filename);
        if (key == null || !visitedKeys.Add(key))
        {
            result._diagnostics.Add(key == null
                ? "Entry has no key"
                : $"Duplicate entry key '{key}' skipped");
            return true;
        }

        var entry = new RawEntry { Key = key };

        foreach (var c in concepts)
            if (!string.IsNullOrEmpty(c))
            {
                entry.Concepts.Add(c);
                entry.ConceptSet.Add(c);
            }

        if (obj.TryGetProperty("$inherits", out var inhProp) && inhProp.ValueKind == JsonValueKind.String)
            entry.Inherits = inhProp.GetString();

        foreach (var prop in obj.EnumerateObject())
        {
            var name = prop.Name;
            if (name == "$concepts" || name == "$key" || name == "$inherits")
                continue;

            entry.RawFields[name] = ToObject(prop.Value);
            entry.FieldNames.Add(name);
        }

        result._entries.Add(entry);
        extracted = true;
        return true;
    }

    private bool TryGetConcept(JsonElement obj, out List<string> concepts)
    {
        concepts = new List<string>();

        if (obj.TryGetProperty("$concepts", out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
                concepts.Add(prop.GetString()!);
            else if (prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        concepts.Add(item.GetString()!);
                }
            }
        }

        return concepts.Count > 0;
    }

    private string? ExtractKey(JsonElement obj, string? parentKey, string? filename)
    {
        if (!string.IsNullOrEmpty(parentKey)) return parentKey;

        if (obj.TryGetProperty("$key", out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();

        return filename;
    }

    private static object? ToObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in element.EnumerateObject())
                {
                    dict[prop.Name] = ToObject(prop.Value);
                }
                return dict;
            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ToObject(item));
                }
                return list;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt32(out var i)) return i;
                if (element.TryGetInt64(out var l)) return l;
                return element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
            default:
                return null;
        }
    }
}
