namespace DataCatalyst.Core;

using System.Collections.Generic;

/// <summary>Unresolved dependency graph of entries.</summary>
public sealed class DataGraph {
	private readonly Dictionary<string, DataEntry> _entries = [];

	/// <summary>All entries in the graph keyed by identifier.</summary>
	public IReadOnlyDictionary<string, DataEntry> Entries => _entries;

	/// <summary>Returns the underlying mutable dictionary for graph builders.</summary>
	internal Dictionary<string, DataEntry> MutableEntries => _entries;

	public DataGraph() { }

	public DataGraph(Dictionary<string, DataEntry> entries) {
		_entries = entries;
	}
}
