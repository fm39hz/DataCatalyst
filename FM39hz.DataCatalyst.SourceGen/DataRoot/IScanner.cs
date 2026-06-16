namespace FM39hz.DataCatalyst.DataRoot;

using System.Collections.Immutable;

public interface IScanner {
    void Scan(string rootPrefix, System.Collections.Generic.IReadOnlyList<(string RelativePath, string Content)> files);
    System.Collections.Generic.IReadOnlyList<SchemaDefinition> Schemas { get; }
    System.Collections.Generic.IReadOnlyList<DataFileDefinition> DataFiles { get; }
    void SetTemplateFields(ImmutableArray<FieldDefinition> fields);
}
