namespace DataCatalyst.DataRoot;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

public abstract class FieldType {
    public abstract string CSharpName { get; }
    public abstract string EmitDefault(LoadHint load, object? value);
    public abstract object? ParseValue(JsonElement element);
    public abstract bool TryInfer(JsonElement element);
    public virtual bool IsComplex => false;
    public virtual void EmitDeclarations(StringBuilder sb, int indent) { }
}

public enum LoadHint { Eager, Lazy }

public sealed class IntFieldType : FieldType {
    public override string CSharpName => "int";
    public override string EmitDefault(LoadHint load, object? value) => value is int i ? i.ToString() : "0";
    public override object? ParseValue(JsonElement e) => e.TryGetInt32(out var i) ? i : (int?)null;
    public override bool TryInfer(JsonElement e) => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out _);
}

public sealed class FloatFieldType : FieldType {
    public override string CSharpName => "float";
    public override string EmitDefault(LoadHint load, object? value) =>
        value is float f ? f.ToString("G") + "f" : value is int i ? i.ToString() + "f" : "0f";
    public override object? ParseValue(JsonElement e) => e.TryGetSingle(out var f) ? f : (float?)null;
    public override bool TryInfer(JsonElement e) => e.ValueKind == JsonValueKind.Number;
}

public sealed class BoolFieldType : FieldType {
    public override string CSharpName => "bool";
    public override string EmitDefault(LoadHint load, object? value) =>
        value is true ? "true" : "false";
    public override object? ParseValue(JsonElement e) =>
        e.ValueKind == JsonValueKind.True ? (object?)true :
        e.ValueKind == JsonValueKind.False ? (object?)false : null;
    public override bool TryInfer(JsonElement e) =>
        e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False;
}

public sealed class StringFieldType : FieldType {
    public override string CSharpName => "string";
    public override string EmitDefault(LoadHint load, object? value) =>
        value is string s ? $"\"{Escape(s)}\"" : "\"\"";
    public override object? ParseValue(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString() : null;
    public override bool TryInfer(JsonElement e) => e.ValueKind == JsonValueKind.String;
    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

public sealed class ArrayFieldType : FieldType {
    public FieldType ElementType { get; }
    public ArrayFieldType(FieldType elementType) => ElementType = elementType;
    public override string CSharpName =>
        $"global::System.Collections.Immutable.ImmutableArray<{ElementType.CSharpName}>";
    public override string EmitDefault(LoadHint load, object? value) =>
        $"global::System.Collections.Immutable.ImmutableArray<{ElementType.CSharpName}>.Empty";
    public override object? ParseValue(JsonElement e) => null;
    public override bool TryInfer(JsonElement e) => e.ValueKind == JsonValueKind.Array;
    public override bool IsComplex => true;
}

public sealed class NestedFieldType : FieldType {
    public ImmutableArray<FieldDefinition> Fields { get; }
    public string StructName { get; }
    public NestedFieldType(string structName, ImmutableArray<FieldDefinition> fields) {
        StructName = structName; Fields = fields;
    }
    public override string CSharpName => StructName;
    public override string EmitDefault(LoadHint load, object? value) => $"new {StructName}()";
    public override object? ParseValue(JsonElement e) => null;
    public override bool TryInfer(JsonElement e) => e.ValueKind == JsonValueKind.Object;
    public override bool IsComplex => true;
    public override void EmitDeclarations(StringBuilder sb, int indent) {
        var ind = new string('\t', indent);
        sb.Append(ind).Append("public partial struct ").Append(StructName).AppendLine(" {");
        foreach (var f in Fields) {
            f.Type.EmitDeclarations(sb, indent + 1);
            sb.Append(ind).Append('\t').Append("public ").Append(f.CSharpType).Append(" ").Append(f.Name)
              .Append(" { get; set; }").AppendLine();
        }
        sb.Append(ind).AppendLine("}");
        sb.AppendLine();
    }
}

public sealed class RefFieldType : FieldType {
    public string Target { get; }
    public RefFieldType(string target) => Target = target;
    public override string CSharpName => $"global::DataCatalyst.Abstractions.DataRef<{Target}>";
    public override string EmitDefault(LoadHint load, object? value) => "default";
    public override object? ParseValue(JsonElement e) => null;
    public override bool TryInfer(JsonElement e) => false;
}

public static class FieldTypeRegistry {
    private static readonly List<FieldType> _types = new() {
        new IntFieldType(), new FloatFieldType(), new BoolFieldType(), new StringFieldType(),
    };
    public static void Register(FieldType type) => _types.Add(type);
    public static FieldType? Infer(JsonElement element) {
        foreach (var t in _types)
            if (t.TryInfer(element)) return t;
        return null;
    }
    public static FieldType? GetNamed(string name) => name switch {
        "int" => new IntFieldType(), "float" => new FloatFieldType(),
        "bool" => new BoolFieldType(), "string" => new StringFieldType(),
        _ => null,
    };
}
