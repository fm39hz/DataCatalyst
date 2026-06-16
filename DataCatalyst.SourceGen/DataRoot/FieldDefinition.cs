namespace DataCatalyst.DataRoot;

using System.Collections.Immutable;

public sealed class FieldDefinition {
    public string Name { get; }
    public FieldType Type { get; }
    public LoadHint Load { get; }
    public object? Default { get; }

    public FieldDefinition(string name, FieldType type, LoadHint load = LoadHint.Eager, object? defaultValue = null) {
        Name = name; Type = type; Load = load; Default = defaultValue;
    }

    public string CSharpType => Load == LoadHint.Lazy
        ? $"global::System.Lazy<{Type.CSharpName}>"
        : Type.CSharpName;

    public string CSharpDefault => Type.EmitDefault(Load, Default);
    public static string NestedTypeName(string column) => column + "Row";
}
