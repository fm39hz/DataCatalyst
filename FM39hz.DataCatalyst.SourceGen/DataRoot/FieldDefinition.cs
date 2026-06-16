namespace FM39hz.DataCatalyst.DataRoot;

using System.Collections.Generic;

public enum LoadHint {
    Eager,
    Lazy,
    Stream,
    Ref,
}

public enum FieldKind {
    Primitive,
    Ref,
    Script,
    Nested,
}

public sealed class FieldDefinition {
    public string Name { get; }
    public string Type { get; }
    public FieldKind Kind { get; }
    public LoadHint Load { get; }
    public string? RefTarget { get; }
    public object? Default { get; }

    public FieldDefinition(string name, string type, FieldKind kind = FieldKind.Primitive,
        LoadHint load = LoadHint.Eager, string? refTarget = null, object? defaultValue = null) {
        Name = name; Type = type; Kind = kind;
        Load = load; RefTarget = refTarget; Default = defaultValue;
    }

    public string CSharpType => Load switch {
        LoadHint.Lazy => $"global::System.Lazy<{Type}>",
        LoadHint.Ref => $"global::FM39hz.DataCatalyst.Abstractions.DataRef<{RefTarget ?? Type}>",
        _ => Type,
    };

    public string CSharpDefault => Load switch {
        LoadHint.Lazy => $"new global::System.Lazy<{Type}>(() => default)",
        _ => Default?.ToString() ?? "default",
    };
}
