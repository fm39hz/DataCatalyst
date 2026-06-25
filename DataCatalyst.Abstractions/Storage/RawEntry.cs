using System;
using System.Collections.Generic;

namespace DataCatalyst.Storage;

public sealed class RawEntry
{
    /// <summary>MergePolicy as int to avoid dependency on Pipeline.MergePolicy enum
    /// (RawEntry is linked into the SourceGen project which doesn't reference Abstractions).
    /// 0=Patch, 1=FieldPatch, 2=Overlay, 3=Replace
    /// </summary>
    public int MergePolicyValue { get; set; }

    public string Key { get; set; } = string.Empty;
    public List<string> Concepts { get; set; } = new();
    public HashSet<string> ConceptSet { get; set; } = new();
    public string? Inherits { get; set; }
    public Dictionary<Type, object> Components { get; set; } = new();
    public Dictionary<string, string> CrossRefs { get; set; } = new();
    public int AssignedIndex { get; set; }
    public List<string> FieldNames { get; set; } = new();
    public Dictionary<string, object?> RawFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
