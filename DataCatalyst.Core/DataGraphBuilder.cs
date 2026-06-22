namespace DataCatalyst.Core;

using DataCatalyst.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

public static class DataGraphBuilder {
	private const string Unknown = "unknown";

	public static DataGraph Build(IEnumerable<DataEntry> entries, List<string>? diagnostics = null, DataCatalystEnvironment? env = null) {
		env ??= DataCatalystEnvironment.Default;
		var graph = new DataGraph();
		var diag = diagnostics ?? [];

		static int GetLayer(DataEntry e) => e.TryGet<Layer>(out var l) ? l.Value : 0;
		var sorted = entries.OrderBy(e => GetLayer(e)).ToList();

		var knownKeys = new HashSet<string>();
		foreach (var entry in sorted) knownKeys.Add(entry.Key);

		foreach (var entry in sorted) {
		entry.TryGet<Inherits>(out var inh);
				if (inh.Value != null) {
					foreach (var parent in inh.Value) {
					if (!knownKeys.Contains(parent))
						diag.Add($"Entry '{entry.Key}' inherits from missing parent '{parent}'.");
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
					var mergedComps = new Dictionary<Type, object>(existing.Components);
					foreach (var kv in entry.Components)
						mergedComps[kv.Key] = kv.Value;
						graph.MutableEntries[entry.Key] = new DataEntry(entry.Key, mergedComps) { SourceFile = entry.SourceFile ?? existing.SourceFile };
				}
			}
			else {
				graph.MutableEntries[entry.Key] = entry;
			}
		}

		foreach (var p in env.Plugins.EnabledPlugins.OfType<IGraphPlugin>())
			p.OnGraphBuilt(graph, diag);

		return graph;
	}
}
