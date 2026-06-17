namespace DataCatalyst.Core;

using System.Collections.Generic;

/// <summary>Unresolved dependency graph of entries.</summary>
public sealed class DataGraph {
	/// <summary>All entries in the graph keyed by identifier.</summary>
	public Dictionary<string, DataEntry> Entries { get; } = [];

	public DataGraph() { }

	public DataGraph(Dictionary<string, DataEntry> entries) {
		Entries = entries;
	}
}
