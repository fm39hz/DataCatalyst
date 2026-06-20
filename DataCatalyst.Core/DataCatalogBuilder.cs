namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Builds a flat, immutable catalog by resolving inheritance.</summary>
public static class DataCatalogBuilder {
	/// <summary>Resolves inheritance and returns a populated catalog.</summary>
	public static DataCatalog Resolve(DataGraph graph, List<string>? diagnostics = null, DataCatalystEnvironment? env = null) {
		env ??= DataCatalystEnvironment.Default;

		var resolved = new Dictionary<string, DataEntry>();
		var ordered = TopologicalSort(graph);

		foreach (var entry in ordered) {
			if (resolved.ContainsKey(entry.Key)) {
				continue;
			}

			var merged = CollectComponents(entry, graph, resolved, []);
			resolved[entry.Key] = new DataEntry(entry.Key, merged, null) { SourceFile = entry.SourceFile };
		}

		var catalog = new DataCatalog(resolved);

		var diag = diagnostics ?? [];
			foreach (var p in env.Plugins.EnabledPlugins.OfType<ICatalogPlugin>()) {
				p.OnCatalogResolved(catalog, diag);
			}

		return catalog;
	}

	private static Dictionary<Type, object> CollectComponents(
		DataEntry entry, DataGraph graph,
		Dictionary<string, DataEntry> resolved,
		HashSet<string> visiting) {

		if (!visiting.Add(entry.Key)) {
			throw new InvalidOperationException($"Cycle detected: {entry.Key}");
		}

		// Start with own components
		var merged = new Dictionary<Type, object>(entry._components);

		if (entry.Inherits != null) {
			foreach (var parentKey in entry.Inherits) {
				if (resolved.TryGetValue(parentKey, out var parentEntry)) {
					CopyMissing(merged, parentEntry._components);
				}
				else if (graph.Entries.TryGetValue(parentKey, out var parentGraphEntry)) {
					var parentMerged = CollectComponents(parentGraphEntry, graph, resolved, visiting);
					resolved[parentKey] = new DataEntry(parentKey, parentMerged, null) { SourceFile = parentGraphEntry.SourceFile };
					CopyMissing(merged, parentMerged);
				}
			}
		}

		visiting.Remove(entry.Key);
		return merged;
	}

	private static void CopyMissing(Dictionary<Type, object> target, Dictionary<Type, object> source) {
		foreach (var (type, val) in source) {
			if (!target.ContainsKey(type)) {
				target[type] = val;
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
