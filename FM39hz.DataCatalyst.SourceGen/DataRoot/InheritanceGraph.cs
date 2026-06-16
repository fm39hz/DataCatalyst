namespace FM39hz.DataCatalyst.DataRoot;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

public sealed class InheritanceGraph {
    private readonly Dictionary<string, DataFileDefinition> _nodes = new();
    private readonly Dictionary<string, SchemaDefinition> _schemas = new();

    public IReadOnlyDictionary<string, DataFileDefinition> Nodes => _nodes;
    public IReadOnlyDictionary<string, SchemaDefinition> Schemas => _schemas;

    public void AddSchema(SchemaDefinition schema) => _schemas[schema.Name] = schema;

    public void AddNode(DataFileDefinition node) {
        _nodes[node.Name] = node;
        node.InheritChain = ResolveChain(node);
    }

    public ImmutableArray<string> ResolveChain(DataFileDefinition node) {
        var chain = new List<string>();
        var seen = new HashSet<string>();
        var current = node.Inherits;

        while (current is not null) {
            if (!seen.Add(current)) {
                // Cycle detected
                break;
            }
            chain.Add(current);
            if (_nodes.TryGetValue(current, out var parent)) {
                current = parent.Inherits;
            } else if (_schemas.ContainsKey(current)) {
                current = null; // schema is root
            } else {
                break; // missing parent
            }
        }

        return chain.ToImmutableArray();
    }

    public ImmutableArray<FieldDefinition> FlattenedFields(DataFileDefinition node) {
        var fields = new List<FieldDefinition>();
        var seen = new HashSet<string>();

        // Walk chain ngược: root → child
        for (var i = node.InheritChain.Length - 1; i >= 0; i--) {
            var parentName = node.InheritChain[i];
            if (_nodes.TryGetValue(parentName, out var parent)) {
                AddFields(fields, seen, parent.Fields);
            } else if (_schemas.TryGetValue(parentName, out var schema)) {
                AddFields(fields, seen, schema.Fields);
            }
        }

        // Own fields (ghi đè)
        AddFields(fields, seen, node.Fields);

        return fields.ToImmutableArray();
    }

    private static void AddFields(List<FieldDefinition> fields, HashSet<string> seen,
        ImmutableArray<FieldDefinition> incoming) {
        foreach (var f in incoming) {
            if (!seen.Add(f.Name)) {
                // Remove old, replace with derived
                fields.RemoveAll(x => x.Name == f.Name);
                fields.Add(f);
            } else {
                fields.Add(f);
            }
        }
    }

    public bool TryGetFlattenedFields(string nodeName, out ImmutableArray<FieldDefinition> fields) {
        fields = ImmutableArray<FieldDefinition>.Empty;
        if (_nodes.TryGetValue(nodeName, out var node)) {
            fields = FlattenedFields(node);
            return true;
        }
        return false;
    }
}
