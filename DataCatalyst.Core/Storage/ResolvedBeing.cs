namespace DataCatalyst.Storage;

using System;
using System.Collections.Generic;

public sealed class ResolvedBeing {
	public string Key { get; }
	public int AssignedIndex { get; internal set; }
	public string? Inherits { get; internal set; }
	public List<int> Concepts { get; } = [];
	public HashSet<int> ConceptSet { get; } = [];
	public Dictionary<Type, object> Components { get; } = [];
	public Dictionary<int, object?> AspectFields { get; } = [];

	internal ResolvedBeing(string key) { Key = key; }
}
