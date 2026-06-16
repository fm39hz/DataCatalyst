namespace FM39hz.DataCatalyst.DataRoot;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;

public sealed class DataRootScanner {
    private readonly List<SchemaDefinition> _schemas = new();
    private readonly List<DataFileDefinition> _dataFiles = new();

    public IReadOnlyList<SchemaDefinition> Schemas => _schemas;
    public IReadOnlyList<DataFileDefinition> DataFiles => _dataFiles;

    public void Scan(string rootPrefix, IReadOnlyList<(string RelativePath, string Content)> files) {
        foreach (var (relativePath, content) in files) {
            if (!relativePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            var fileName = System.IO.Path.GetFileNameWithoutExtension(relativePath);
            if (fileName.StartsWith("_"))
                ParseSchema(relativePath, content);
            else
                ParseDataFile(relativePath, content);
        }
    }

    private void ParseSchema(string relativePath, string content) {
        var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var name = System.IO.Path.GetFileNameWithoutExtension(relativePath).TrimStart('_');
        var ns = PathToNamespace(relativePath);
        var kind = root.TryGetProperty("kind", out var k) ? k.GetString() ?? name : name;
        _schemas.Add(new SchemaDefinition(kind, ns, relativePath, ParseFields(root)));
    }

    private void ParseDataFile(string relativePath, string content) {
        var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var name = System.IO.Path.GetFileNameWithoutExtension(relativePath);
        var ns = PathToNamespace(relativePath);
        var inherits = root.TryGetProperty("inherits", out var inh) ? inh.GetString() : null;
        var load = root.TryGetProperty("load", out var l) ? l.GetString() : "startup";
        _dataFiles.Add(new DataFileDefinition(name, ns, relativePath, inherits,
            ParseFields(root), ParseDefaults(root),
            isCompileEager: load == "compile"));
    }

    private static ImmutableArray<FieldDefinition> ParseFields(JsonElement root) {
        if (!root.TryGetProperty("fields", out var fEl))
            return ImmutableArray<FieldDefinition>.Empty;
        var builder = ImmutableArray.CreateBuilder<FieldDefinition>();
        foreach (var prop in fEl.EnumerateObject()) {
            var f = ParseField(prop);
            if (f is not null) builder.Add(f);
        }
        return builder.ToImmutable();
    }

    private static FieldDefinition? ParseField(JsonProperty prop) {
        var obj = prop.Value;
        var type = obj.TryGetProperty("type", out var t) ? t.GetString() ?? "string" : "string";
        var kind = type switch {
            "ref" => FieldKind.Ref,
            "script" => FieldKind.Script,
            _ when obj.TryGetProperty("fields", out _) => FieldKind.Nested,
            _ => FieldKind.Primitive,
        };
        var loadStr = obj.TryGetProperty("load", out var l) ? l.GetString() : "eager";
        var load = loadStr switch {
            "lazy" => LoadHint.Lazy,
            "stream" => LoadHint.Stream,
            "ref" => LoadHint.Ref,
            _ => LoadHint.Eager,
        };
        var refTarget = obj.TryGetProperty("target", out var r) ? r.GetString() : null;
        object? defaultValue = null;
        if (obj.TryGetProperty("default", out var d)) {
            defaultValue = type switch {
                "int" when d.TryGetInt32(out var i) => i,
                "float" when d.TryGetSingle(out var f) => f,
                "bool" when d.ValueKind == JsonValueKind.True => (object?)true,
                "bool" when d.ValueKind == JsonValueKind.False => (object?)false,
                _ when d.ValueKind == JsonValueKind.String => d.GetString(),
                _ => null,
            };
        }
        return new FieldDefinition(prop.Name, type, kind, load, refTarget, defaultValue);
    }

    private static ImmutableDictionary<string, object?> ParseDefaults(JsonElement root) {
        if (!root.TryGetProperty("defaults", out var defs))
            return ImmutableDictionary<string, object?>.Empty;
        var builder = ImmutableDictionary.CreateBuilder<string, object?>();
        foreach (var prop in defs.EnumerateObject()) {
            builder[prop.Name] = prop.Value.ValueKind switch {
                JsonValueKind.Number when prop.Value.TryGetInt32(out var i) => i,
                JsonValueKind.Number when prop.Value.TryGetSingle(out var f) => f,
                JsonValueKind.True => (object?)true,
                JsonValueKind.False => (object?)false,
                JsonValueKind.String => prop.Value.GetString(),
                _ => null,
            };
        }
        return builder.ToImmutable();
    }

    public static string PathToNamespace(string relativePath) {
        var dir = System.IO.Path.GetDirectoryName(relativePath) ?? "";
        var parts = dir.Split('/', '\\');
        return string.Join(".", parts.Where(p => p.Length > 0));
    }
}
