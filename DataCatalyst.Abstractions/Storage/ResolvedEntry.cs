using System;
using System.Collections.Generic;

namespace DataCatalyst.Storage;

public sealed class ResolvedEntry
{
    public string Key { get; set; } = string.Empty;
    public int AssignedIndex { get; set; }
    public string? Inherits { get; set; }
    public List<int> Concepts { get; set; } = new();
    public HashSet<int> ConceptSet { get; set; } = new();
    public Dictionary<Type, object> Components { get; set; } = new();
    public Dictionary<int, object?> AspectFields { get; set; } = new();
}
