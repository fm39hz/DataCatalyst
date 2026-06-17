namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

/// <summary>Builds a flat, immutable catalog by resolving inheritance.</summary>
public static class DataCatalogBuilder {
	/// <summary>Resolves inheritance and returns a populated catalog.</summary>
	public static DataCatalog Resolve(DataGraph graph) {
		var resolved = new Dictionary<string, DataEntry>();
		var ordered = TopologicalSort(graph);

		foreach (var entry in ordered) {
			if (entry._resolved) {
				continue;
			}

			ResolveEntry(entry, graph, resolved, []);
		}

		return new DataCatalog(resolved);
	}

	private static void ResolveEntry(
		DataEntry entry, DataGraph graph,
		Dictionary<string, DataEntry> resolved,
		HashSet<string> visiting) {

		if (resolved.ContainsKey(entry.Key)) {
			return;
		}

		if (!visiting.Add(entry.Key)) {
			throw new InvalidOperationException($"Cycle detected: {entry.Key}");
		}

		if (entry.Inherits != null) {
			foreach (var parentKey in entry.Inherits) {
				if (resolved.TryGetValue(parentKey, out var parentEntry)) {
					MergeComponents(entry, parentEntry);
				}
				else if (graph.Entries.TryGetValue(parentKey, out var parentGraphEntry)) {
					ResolveEntry(parentGraphEntry, graph, resolved, visiting);
					if (resolved.TryGetValue(parentKey, out var resolvedParent)) {
						MergeComponents(entry, resolvedParent);
					}
				}
			}
		}

		visiting.Remove(entry.Key);
		entry._resolved = true;
		resolved[entry.Key] = entry;
	}

	private static void MergeComponents(DataEntry child, DataEntry parent) {
		foreach (var (type, val) in parent._components) {
			if (!child._components.ContainsKey(type)) {
				child._components[type] = val;
			}
		}
	}

	private static List<DataEntry> TopologicalSort(DataGraph graph) {
		var visited = new HashSet<string>();
		var result = new List<DataEntry>();

		void Dfs(DataEntry entry) {
			if (!visited.Add(entry.Key)) {
				return;
			}

			if (entry.Inherits != null) {
				foreach (var parentKey in entry.Inherits) {
					if (graph.Entries.TryGetValue(parentKey, out var parent)) {
						Dfs(parent);
					}
				}
			}
			result.Add(entry);
		}

		foreach (var entry in graph.Entries.Values) {
			Dfs(entry);
		}

		return result;
	}
}
