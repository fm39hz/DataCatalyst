namespace FM39hz.DataCatalyst.DataRoot;

using System.Collections.Generic;
using System.Collections.Immutable;

public interface IGraphBuilder {
    IReadOnlyDictionary<string, DataFileDefinition> Nodes { get; }
    IReadOnlyDictionary<string, SchemaDefinition> Schemas { get; }
    void AddSchema(SchemaDefinition schema);
    void AddNode(DataFileDefinition node);
    ImmutableArray<FieldDefinition> FlattenedFields(DataFileDefinition node);
}
