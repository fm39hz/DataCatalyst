namespace DataCatalyst.Core;

using System.Collections.Generic;
using System.Linq;
using Abstractions;

/// <summary>Builds data graphs from entry collections.</summary>
public static class DataGraphBuilder {
	/// <summary>Creates a graph from the given entries, merging components for duplicate keys.</summary>
	public static DataGraph Build(IEnumerable<DataEntry> entries, List<string>? diagnostics = null) {
		var graph = new DataGraph();
		foreach (var entry in entries) {
			if (graph.Entries.TryGetValue(entry.Key, out var existing)) {
				diagnostics?.Add(
					$"Entry '{entry.Key}' from '{entry.SourceFile ?? "unknown"}' overrides/merges components of existing entry from '{existing.SourceFile ?? "unknown"}'.");
				foreach (var (type, val) in entry.Components) {
					existing._components[type] = val;
				}

				if (entry.Inherits != null) {
					existing.Inherits = entry.Inherits;
				}
			}
			else {
				graph.Entries[entry.Key] = entry;
			}
		}

		var diag = diagnostics ?? new List<string>();
		foreach (var p in PluginRegistry.Plugins.OfType<IGraphPlugin>()) {
			p.OnGraphBuilt(graph, diag);
		}

		return graph;
	}
}
