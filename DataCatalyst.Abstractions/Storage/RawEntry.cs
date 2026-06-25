namespace DataCatalyst.Storage;

using System;
using System.Collections.Generic;

public sealed class RawEntry {
	public int MergePolicyValue { get; set; }
	public string Key { get; set; } = string.Empty;
	public List<string> Concepts { get; set; } = [];
	public HashSet<string> ConceptSet { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public string? Inherits { get; set; }
	public Dictionary<Type, object> Components { get; set; } = [];
	public Dictionary<string, object?> RawFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public List<string> FieldNames { get; set; } = [];
	public int AssignedIndex { get; set; }
}
