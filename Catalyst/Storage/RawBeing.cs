namespace Catalyst.Storage;

using System;
using System.Collections.Generic;
using Catalyst.Pipeline;

public sealed class RawBeing(string key) {
	public MergePolicy MergePolicyValue { get; set; } = MergePolicy.Patch;
	public string Key { get; } = key;
	public List<string> Concepts { get; } = [];
	public HashSet<string> ConceptSet { get; } = new(StringComparer.OrdinalIgnoreCase);
	public string? Inherits { get; internal set; }
	public Dictionary<Type, object> Components { get; } = [];
	public Dictionary<string, object?> RawFields { get; } = new(StringComparer.OrdinalIgnoreCase);
	public List<string> FieldNames { get; } = [];
	public int AssignedIndex { get; internal set; }
}
