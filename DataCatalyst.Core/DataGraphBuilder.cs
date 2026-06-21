namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Builds data graphs from entry collections with layer-aware merge and dependency tracking.</summary>
public static class DataGraphBuilder {
	private const string Unknown = "unknown";

	/// <summary>Creates a graph from the given entries, merging components for duplicate keys.</summary>
	public static DataGraph Build(IEnumerable<DataEntry> entries, List<string>? diagnostics = null, DataCatalystEnvironment? env = null) {
		env ??= DataCatalystEnvironment.Default;
		var graph = new DataGraph();
		var diag = diagnostics ?? [];

		// Sort by layer ascending — higher layers override lower layers
		var sorted = entries.OrderBy(e => e.Layer).ToList();

		// First pass: collect all known keys for dependency validation
		var knownKeys = new HashSet<string>();
		foreach (var entry in sorted) {
			knownKeys.Add(entry.Key);
		}

		foreach (var entry in sorted) {
			// Validate dependency targets exist
			if (entry.Inherits != null) {
				foreach (var parent in entry.Inherits) {
					if (!knownKeys.Contains(parent)) {
						diag.Add($"Entry '{entry.Key}' inherits from missing parent '{parent}'.");
					}
				}
			}

			if (graph.Entries.TryGetValue(entry.Key, out var existing)) {
				// Higher layer completely overrides lower layer entry
				if (entry.Layer > existing.Layer) {
					diag.Add($"Entry '{entry.Key}' (layer {entry.Layer}) replaces entry from layer {existing.Layer} ('{existing.SourceFile ?? Unknown}').");
					graph.MutableEntries[entry.Key] = entry;
				}
				else {
					// Same layer — create new entry with merged components (immutable)
					diag.Add($"Entry '{entry.Key}' from '{entry.SourceFile ?? Unknown}' overrides/merges components of existing entry from '{existing.SourceFile ?? Unknown}'.");
					var merged = new Dictionary<Type, object>(existing.Components);
					foreach (var (type, val) in entry.Components) {
						merged[type] = val;
					}
					graph.MutableEntries[entry.Key] = new DataEntry(entry.Key, merged,
						entry.Inherits ?? existing.Inherits, entry.ConceptName ?? existing.ConceptName) {
						SourceFile = entry.SourceFile ?? existing.SourceFile,
						Layer = entry.Layer
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
