namespace DataCatalyst.Core;

using DataCatalyst.Abstractions;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Builds data graphs from entry collections with layer-aware merge and dependency tracking.</summary>
public static class DataGraphBuilder {
	private const string Unknown = "unknown";

	private static int GetLayer(DataEntry e) => e.Meta.TryGetValue("Layer", out var v) && v is int i ? i : 0;
	private static string[]? GetInherits(DataEntry e) => e.Meta.TryGetValue("inherits", out var v) && v is string[] arr ? arr : null;
	private static string? GetConcept(DataEntry e) => e.Meta.TryGetValue("Concept", out var v) && v is string s ? s : null;

	/// <summary>Creates a graph from the given entries, merging components for duplicate keys.</summary>
	public static DataGraph Build(IEnumerable<DataEntry> entries, List<string>? diagnostics = null, DataCatalystEnvironment? env = null) {
		env ??= DataCatalystEnvironment.Default;
		var graph = new DataGraph();
		var diag = diagnostics ?? [];

		var sorted = entries.OrderBy(GetLayer).ToList();

		var knownKeys = new HashSet<string>();
		foreach (var entry in sorted) {
			knownKeys.Add(entry.Key);
		}

		foreach (var entry in sorted) {
			var inherits = GetInherits(entry);
			if (inherits != null) {
				foreach (var parent in inherits) {
					if (!knownKeys.Contains(parent)) {
						diag.Add($"Entry '{entry.Key}' inherits from missing parent '{parent}'.");
					}
				}
			}

			if (graph.Entries.TryGetValue(entry.Key, out var existing)) {
				var entryLayer = GetLayer(entry);
				var existingLayer = GetLayer(existing);

				if (entryLayer > existingLayer) {
					diag.Add($"Entry '{entry.Key}' (layer {entryLayer}) replaces entry from layer {existingLayer} ('{entry.SourceFile ?? Unknown}').");
					graph.MutableEntries[entry.Key] = entry;
				}
				else {
					diag.Add($"Entry '{entry.Key}' from '{entry.SourceFile ?? Unknown}' overrides/merges components of existing entry from '{existing.SourceFile ?? Unknown}'.");
					var merged = new Dictionary<Type, object>(existing.MutableComponents);
					foreach (var (type, val) in entry.MutableComponents) {
						merged[type] = val;
					}
					var mergedMeta = new Dictionary<string, object>(existing.Meta);
					foreach (var (k, v) in entry.Meta) {
						mergedMeta[k] = v;
					}
					graph.MutableEntries[entry.Key] = new DataEntry(entry.Key, merged, mergedMeta) {
						SourceFile = entry.SourceFile ?? existing.SourceFile
					};
				}
			}
			else {
				graph.MutableEntries[entry.Key] = entry;
			}
		}

		foreach (var p in env.Plugins.EnabledPlugins.OfType<IGraphPlugin>()) {
			p.OnGraphBuilt(graph, diag);
		}

		return graph;
	}
}
