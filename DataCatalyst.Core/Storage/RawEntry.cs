using System;
using System.Collections.Generic;

namespace DataCatalyst.Storage;

internal sealed class RawEntry
{
    public string Key { get; set; } = string.Empty;
    public List<string> Concepts { get; set; } = new();
    public HashSet<string> ConceptSet { get; set; } = new();
    public string? Inherits { get; set; }
    public Dictionary<Type, object> Components { get; set; } = new();
    public Dictionary<string, string> CrossRefs { get; set; } = new();
    public int AssignedIndex { get; set; }
    public List<string> _fieldNames = new();
    public Dictionary<string, object?> _rawFields = new();
}
