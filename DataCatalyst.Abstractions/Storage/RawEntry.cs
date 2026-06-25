using System;
using System.Collections.Generic;

namespace DataCatalyst.Storage;

public sealed class RawEntry
{
    public int MergePolicyValue { get; set; }
    public string Key { get; set; } = string.Empty;
    public List<string> Concepts { get; set; } = new();
    public HashSet<string> ConceptSet { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Inherits { get; set; }
    public Dictionary<Type, object> Components { get; set; } = new();
    public Dictionary<string, object?> RawFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> FieldNames { get; set; } = new();
    public int AssignedIndex { get; set; }
}
