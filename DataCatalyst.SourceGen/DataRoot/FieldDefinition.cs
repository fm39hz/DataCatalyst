namespace DataCatalyst.DataRoot;

using System.Collections.Generic;
using System.Collections.Immutable;

public enum LoadHint { Eager, Lazy, Stream, Ref }

public enum FieldKind { Primitive, Ref, Script, Nested, Array }

public sealed class FieldDefinition {
    public string Name { get; }
    public string Type { get; }
    public FieldKind Kind { get; }
    public LoadHint Load { get; }
    public string? RefTarget { get; }
    public object? Default { get; }
    public FieldDefinition? ElementType { get; }
    public ImmutableArray<FieldDefinition> NestedFields { get; }

    public FieldDefinition(string name, string type, FieldKind kind = FieldKind.Primitive,
        LoadHint load = LoadHint.Eager, string? refTarget = null, object? defaultValue = null,
        FieldDefinition? elementType = null,
        ImmutableArray<FieldDefinition> nestedFields = default) {
        Name = name; Type = type; Kind = kind; Load = load;
        RefTarget = refTarget; Default = defaultValue;
        ElementType = elementType;
        NestedFields = nestedFields.IsDefault ? ImmutableArray<FieldDefinition>.Empty : nestedFields;
    }

    public string CSharpType => (Kind, Load) switch {
        (FieldKind.Array, _) when ElementType is not null
            => $"global::System.Collections.Immutable.ImmutableArray<{ElementType.CSharpType}>",
        (FieldKind.Nested, _) => NestedTypeName(Name),
        (FieldKind.Ref, _) => $"global::DataCatalyst.Abstractions.DataRef<{RefTarget ?? Type}>",
        (_, LoadHint.Lazy) => $"global::System.Lazy<{Type}>",
        _ => Type,
    };

    public string CSharpDefault => (Kind, Load) switch {
        (FieldKind.Array, _) => $"global::System.Collections.Immutable.ImmutableArray<{ElementType?.CSharpType ?? Type}>.Empty",
        (FieldKind.Nested, _) => $"new {NestedTypeName(Name)}()",
        (_, LoadHint.Lazy) => $"new global::System.Lazy<{Type}>(() => default)",
        _ => Default?.ToString() ?? "default",
    };

    public static string NestedTypeName(string column) => column + "Row";
}
