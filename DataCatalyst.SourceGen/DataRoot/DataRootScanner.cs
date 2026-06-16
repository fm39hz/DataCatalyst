namespace DataCatalyst.DataRoot;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;

public sealed class DataRootScanner : IScanner {
    private readonly List<SchemaDefinition> _schemas = new();
    private readonly List<DataFileDefinition> _dataFiles = new();
    private ImmutableArray<FieldDefinition> _templateFields = ImmutableArray<FieldDefinition>.Empty;

    public IReadOnlyList<SchemaDefinition> Schemas => _schemas;
    public IReadOnlyList<DataFileDefinition> DataFiles => _dataFiles;
    public void SetTemplateFields(ImmutableArray<FieldDefinition> fields) => _templateFields = fields;

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

        var dslId = root.TryGetProperty("$dsl", out var dslEl) ? dslEl.GetString() : null;
        if (dslId is not null) {
            _dataFiles.Add(new DataFileDefinition(name, ns, relativePath, null,
                ImmutableArray<FieldDefinition>.Empty, ImmutableDictionary<string, object?>.Empty,
                dslId: dslId));
            return;
        }

        var inherits = root.TryGetProperty("inherits", out var inh) ? inh.GetString() : null;
        var load = root.TryGetProperty("load", out var l) ? l.GetString() : "startup";

        var mergedFields = ImmutableArray.CreateBuilder<FieldDefinition>();
        var seen = new HashSet<string>();
        foreach (var tf in _templateFields) { seen.Add(tf.Name); mergedFields.Add(tf); }
        foreach (var jf in ParseFields(root)) {
            if (seen.Contains(jf.Name)) continue;
            mergedFields.Add(jf);
        }

        _dataFiles.Add(new DataFileDefinition(name, ns, relativePath, inherits,
            mergedFields.ToImmutable(), ParseDefaults(root),
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
        var typeStr = obj.TryGetProperty("type", out var t) ? t.GetString() : null;
        var loadStr = obj.TryGetProperty("load", out var l) ? l.GetString() : "eager";
        var load = loadStr == "lazy" ? LoadHint.Lazy : LoadHint.Eager;

        FieldType? fieldType = null;

        // Explicit type
        if (typeStr is not null) {
            fieldType = typeStr switch {
                "array" when obj.TryGetProperty("element", out var el) => ParseArrayType(el),
                "object" => ParseNestedType(obj, prop.Name),
                "ref" when obj.TryGetProperty("target", out var rt) => new RefFieldType(rt.GetString() ?? "object"),
                _ => FieldTypeRegistry.GetNamed(typeStr),
            };
        }

        // Inference from value
        if (fieldType is null && obj.TryGetProperty("default", out var dVal)) {
            fieldType = FieldTypeRegistry.Infer(dVal);
        }

        // Fallback
        if (fieldType is null) {
            // Check if it has sub-fields (nested object)
            if (obj.TryGetProperty("fields", out _))
                fieldType = ParseNestedType(obj, prop.Name);
            else
                fieldType = new StringFieldType();
        }

        object? defaultValue = null;
        if (obj.TryGetProperty("default", out var def)) {
            defaultValue = fieldType.ParseValue(def);
        }

        return new FieldDefinition(prop.Name, fieldType, load, defaultValue);
    }

    private static FieldType ParseArrayType(JsonElement el) {
        var elemType = el.TryGetProperty("type", out var et) ? et.GetString() : "string";
        var inner = FieldTypeRegistry.GetNamed(elemType) ?? new StringFieldType();
        return new ArrayFieldType(inner);
    }

    private static FieldType ParseNestedType(JsonElement obj, string propName) {
        if (!obj.TryGetProperty("fields", out var fieldsEl)) {
            // Try to infer from default object value
            if (obj.TryGetProperty("default", out var d) && d.ValueKind == JsonValueKind.Object) {
                var fb = ImmutableArray.CreateBuilder<FieldDefinition>();
                foreach (var sub in d.EnumerateObject()) {
                    var inferred = FieldTypeRegistry.Infer(sub.Value) ?? new StringFieldType();
                    fb.Add(new FieldDefinition(sub.Name, inferred));
                }
                return new NestedFieldType(FieldDefinition.NestedTypeName(propName), fb.ToImmutable());
            }
            return new NestedFieldType(FieldDefinition.NestedTypeName(propName), ImmutableArray<FieldDefinition>.Empty);
        }
        var builder = ImmutableArray.CreateBuilder<FieldDefinition>();
        foreach (var sub in fieldsEl.EnumerateObject()) {
            var sf = ParseField(sub);
            if (sf is not null) builder.Add(sf);
        }
        return new NestedFieldType(FieldDefinition.NestedTypeName(propName), builder.ToImmutable());
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
