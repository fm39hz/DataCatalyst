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
    public LoaderAbstractions.LoadResult LoadFile(string path)
    {
        var result = new LoaderAbstractions.LoadResult();
        try
        {
            var text = File.ReadAllText(path);
            var key = Path.GetFileNameWithoutExtension(path);
            ParseJson(text, key, result);
        }
        catch (Exception ex)
        {
            result._diagnostics.Add($"Error loading '{path}': {ex.Message}");
        }
        return result;
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
                    if (prop.Name == "Concept" || prop.Name == "concept" ||
                        prop.Name == "$key" || prop.Name == "Id" || prop.Name == "_id" ||
                        prop.Name == "Inherits" || prop.Name == "inherits")
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

    private static readonly JsonEncodedText ConceptProp = JsonEncodedText.Encode("Concept");
    private static readonly JsonEncodedText ConceptPropLower = JsonEncodedText.Encode("concept");

    private bool TryExtractEntry(JsonElement obj, string? parentKey, string? filename,
        LoaderAbstractions.LoadResult result, HashSet<string> visitedKeys,
        out bool extracted)
    {
        extracted = false;

        // Check if this object has a "Concept" field
        if (!TryGetConcept(obj, out var concepts) || concepts.Count == 0)
            return false;

        // Extract entry key
        var key = ExtractKey(obj, parentKey, filename);
        if (key == null || !visitedKeys.Add(key))
        {
            result._diagnostics.Add(key == null
                ? "Entry has no key"
                : $"Duplicate entry key '{key}' skipped");
            return true; // Was an entry, but couldn't extract
        }

        var entry = new RawEntry { Key = key };

        // Parse concepts
        foreach (var c in concepts)
            if (!string.IsNullOrEmpty(c))
            {
                entry.Concepts.Add(c);
                entry.ConceptSet.Add(c);
            }

        // Parse Inherits
        if (obj.TryGetProperty("Inherits", out var inhProp) && inhProp.ValueKind == JsonValueKind.String)
            entry.Inherits = inhProp.GetString();
        else if (obj.TryGetProperty("inherits", out inhProp) && inhProp.ValueKind == JsonValueKind.String)
            entry.Inherits = inhProp.GetString();

        // Parse components
        foreach (var prop in obj.EnumerateObject())
        {
            var name = prop.Name;
            if (name == "Concept" || name == "concept" ||
                name == "$key" || name == "Id" || name == "_id" ||
                name == "Inherits" || name == "inherits")
                continue;

            var rawJson = prop.Value.GetRawText();
            entry._rawFields[name] = rawJson;
            entry._fieldNames.Add(name);
            entry.CrossRefs[name] = rawJson;
            if (DataCatalyst.Storage.AspectTypeRegistry.TryGetType(name, out var aspectType))
            {
                try {
                    var d = System.Text.Json.JsonSerializer.Deserialize(rawJson, aspectType);
                    if (d != null) {
                        entry.Components[aspectType] = d;
                        System.Console.Error.WriteLine($"[TRACE-JSON] stored {name} as {aspectType.Name}: {d}");
                    } else {
                        System.Console.Error.WriteLine($"[TRACE-JSON] {name} deserialized to null");
                    }
                } catch (Exception ex) {
                    System.Console.Error.WriteLine($"[TRACE-JSON] {name} error: {ex.GetType().Name}: {ex.Message}");
                }
            } else {
                System.Console.Error.WriteLine($"[TRACE-JSON] {name} type not found by AspectTypeRegistry");
            }
        }

        result._entries.Add(entry);
        extracted = true;
        return true;
    }

    private bool TryGetConcept(JsonElement obj, out List<string> concepts)
    {
        concepts = new List<string>();

        if (obj.TryGetProperty("Concept", out var prop) ||
            obj.TryGetProperty("concept", out prop))
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
        // Priority: parentKey > $key > _id > Id > Name > Key > filename
        if (!string.IsNullOrEmpty(parentKey)) return parentKey;

        string[] keyFields = { "$key", "_id", "Id", "id", "Name", "name", "Key", "key" };
        foreach (var field in keyFields)
        {
            if (obj.TryGetProperty(field, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
        }

        return filename;
    }
}
