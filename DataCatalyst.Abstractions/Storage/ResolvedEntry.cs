using System;
using System.Collections.Generic;
using DataCatalyst.Storage;

namespace DataCatalyst.Storage;

/// <summary>Entry after cross-references are resolved and aspects are deserialized.
/// Immutable-style data for the Build phase.</summary>
public sealed class ResolvedEntry
{
    public string Key { get; set; } = string.Empty;
    public List<string> Concepts { get; set; } = new();
    public HashSet<string> ConceptSet { get; set; } = new();
    public string? Inherits { get; set; }
    public Dictionary<Type, object> Components { get; set; } = new();
    public Dictionary<string, string> CrossRefs { get; set; } = new();
    public int AssignedIndex { get; set; }
}
