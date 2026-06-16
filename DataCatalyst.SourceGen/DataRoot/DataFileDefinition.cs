namespace DataCatalyst.DataRoot;

using System.Collections.Generic;
using System.Collections.Immutable;

public sealed class DataFileDefinition {
    public string Name { get; }
    public string Namespace { get; }
    public string FilePath { get; }
    public string? Inherits { get; }
    public string? DslId { get; }
    public bool IsDslFile => DslId is not null;
    public ImmutableArray<string> InheritChain { get; set; }
    public ImmutableArray<FieldDefinition> Fields { get; }
    public ImmutableDictionary<string, object?> Defaults { get; }
    public bool IsCompileEager { get; }

    public DataFileDefinition(string name, string ns, string filePath,
        string? inherits, ImmutableArray<FieldDefinition> fields,
        ImmutableDictionary<string, object?> defaults,
        bool isCompileEager = false, string? dslId = null) {
        Name = name; Namespace = ns; FilePath = filePath;
        Inherits = inherits; Fields = fields; Defaults = defaults;
        IsCompileEager = isCompileEager;
        InheritChain = ImmutableArray<string>.Empty;
    }
}
