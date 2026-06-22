namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Abstractions;

/// <summary>Builds a flat, immutable catalog by resolving inheritance.</summary>
public static class DataCatalogBuilder {
	private static string[]? GetInherits(DataEntry e) => e.Meta.TryGetValue("inherits", out var v) && v is string[] arr ? arr : null;
	private static string? GetConcept(DataEntry e) => e.Meta.TryGetValue("Concept", out var v) && v is string s ? s : null;

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
			resolved[entry.Key] = new DataEntry(entry.Key, merged, new Dictionary<string, object>(entry.Meta)) {
				SourceFile = entry.SourceFile
			};
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

		var merged = new Dictionary<Type, object>(entry.MutableComponents);

		var inherits = GetInherits(entry);
		if (inherits != null) {
			foreach (var parentKey in inherits) {
				if (resolved.TryGetValue(parentKey, out var parentEntry)) {
					CopyMissing(merged, parentEntry.Components);
				}
				else if (graph.Entries.TryGetValue(parentKey, out var parentGraphEntry)) {
					var parentMerged = CollectComponents(parentGraphEntry, graph, resolved, visiting);
					resolved[parentKey] = new DataEntry(parentKey, parentMerged, new Dictionary<string, object>(parentGraphEntry.Meta)) {
						SourceFile = parentGraphEntry.SourceFile
					};
					CopyMissing(merged, parentMerged);
				}
			}
		}

		visiting.Remove(entry.Key);
		return merged;
	}

	private static void CopyMissing(Dictionary<Type, object> target, IReadOnlyDictionary<Type, object> source) {
		foreach (var (type, inheritedVal) in source) {
			if (target.TryGetValue(type, out var existing)) {
				target[type] = ComponentMerger.Merge(type, existing, inheritedVal);
			}
			else {
				target[type] = inheritedVal;
			}
		}
	}

	private static List<DataEntry> TopologicalSort(DataGraph graph) {
		const int Gray = 1;
		const int Black = 2;

		var colors = new Dictionary<string, int>();
		var result = new List<DataEntry>();

		void Dfs(DataEntry entry) {
			colors.TryGetValue(entry.Key, out var color);

			if (color == Black) return;
			if (color == Gray)
				throw new InvalidOperationException($"Cycle detected in inheritance graph: '{entry.Key}' appears more than once in the same chain.");

			colors[entry.Key] = Gray;

			var inherits = GetInherits(entry);
			if (inherits != null) {
				foreach (var parentKey in inherits) {
					if (graph.Entries.TryGetValue(parentKey, out var parent)) {
						Dfs(parent);
					}
				}
			}

			colors[entry.Key] = Black;
			result.Add(entry);
		}

		foreach (var entry in graph.Entries.Values) {
			Dfs(entry);
		}

		return result;
	}
}
