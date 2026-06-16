namespace FM39hz.DataCatalyst.DataRoot;

using System.Collections.Generic;
using System.Collections.Immutable;

public sealed class SchemaDefinition {
    public string Name { get; }
    public string Namespace { get; }
    public string FilePath { get; }
    public ImmutableArray<FieldDefinition> Fields { get; }

    public SchemaDefinition(string name, string ns, string filePath,
        ImmutableArray<FieldDefinition> fields) {
        Name = name; Namespace = ns; FilePath = filePath; Fields = fields;
    }
}
