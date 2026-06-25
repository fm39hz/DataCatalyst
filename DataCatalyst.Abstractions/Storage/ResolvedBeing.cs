namespace DataCatalyst.Storage;

using System;
using System.Collections.Generic;

public sealed class ResolvedBeing {
	public string Key { get; set; } = string.Empty;
	public int AssignedIndex { get; set; }
	public string? Inherits { get; set; }
	public List<int> Concepts { get; set; } = [];
	public HashSet<int> ConceptSet { get; set; } = [];
	public Dictionary<Type, object> Components { get; set; } = [];
	public Dictionary<int, object?> AspectFields { get; set; } = [];
}
