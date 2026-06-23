namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Abstractions;

/// <summary>
/// Dựng DataGraph và OverlayGraph từ các DataEntry theo các MergePolicy khác nhau.
/// </summary>
public static class PolicyGraphBuilder {
	/// <summary>
	/// Xây dựng Main Graph và Overlay Graph từ các entries đã được đính kèm nguồn và sắp xếp.
	/// </summary>
	public static (DataGraph Main, OverlayGraph Overlay) Build(
		IEnumerable<(DataEntry Entry, DataSource Source)> entriesWithSource,
		List<string> diagnostics) {

		var mainGraph = new DataGraph();
		var overlayGraph = new OverlayGraph();
		var conflictLog = new ConflictLog(diagnostics);

		// Thư viện lookup tất cả key đã biết để cảnh báo kế thừa sai
		var knownKeys = new HashSet<string>(StringComparer.Ordinal);

		foreach (var (entry, source) in entriesWithSource) {
			knownKeys.Add(entry.Key);

			// Kiểm tra Scope
			if (source.Scope != null && !IsInScope(entry, source.Scope)) {
				continue;
			}

			// Lấy hoặc tính toán Layer fallback của entry nếu dùng legacy JSON layer
			int entryPriority = source.Priority;
			if (entryPriority == 0 && entry.TryGet<Layer>(out var l)) {
				entryPriority = l.Value;
			}

			switch (source.MergePolicy) {
				case MergePolicy.Additive:
					ApplyAdditive(entry, mainGraph);
					break;

				case MergePolicy.Patch:
					ApplyPatch(entry, source, entryPriority, mainGraph, conflictLog);
					break;

				case MergePolicy.FieldPatch:
					ApplyFieldPatch(entry, source, entryPriority, mainGraph, conflictLog);
					break;

				case MergePolicy.Replace:
					ApplyReplace(entry, source, mainGraph, conflictLog);
					break;

				case MergePolicy.Overlay:
					overlayGraph.Add(entry, source);
					break;
			}
		}

		// Kiểm tra kế thừa sai mục tiêu (như inherits từ parent không tồn tại)
		foreach (var entry in mainGraph.Entries.Values) {
			if (entry.TryGet<Inherits>(out var inh) && inh.Value != null) {
				foreach (var parentKey in inh.Value) {
					if (!knownKeys.Contains(parentKey)) {
						diagnostics.Add($"Entry '{entry.Key}' inherits from missing parent '{parentKey}'.");
					}
				}
			}
		}

		return (mainGraph, overlayGraph);
	}

	private static bool IsInScope(DataEntry entry, IReadOnlyList<string> scope) {
		if (entry.TryGet<Concept>(out var concept) && concept.Value != null) {
			foreach (var c in concept.Value) {
				if (scope.Contains(c)) return true;
			}
		}
		return false;
	}

	private static void ApplyAdditive(DataEntry entry, DataGraph graph) {
		if (!graph.Entries.ContainsKey(entry.Key)) {
			graph.MutableEntries[entry.Key] = entry;
		}
	}

	private static void ApplyPatch(DataEntry entry, DataSource source, int priority, DataGraph graph, ConflictLog log) {
		if (!graph.Entries.TryGetValue(entry.Key, out var existing)) {
			graph.MutableEntries[entry.Key] = entry;
			log.RecordWrite(entry.Key, source, entry.Components.Keys);
			return;
		}

		int existingPriority = GetExistingPriority(existing);
		if (priority > existingPriority) {
			graph.MutableEntries[entry.Key] = entry;
			log.RecordWrite(entry.Key, source, entry.Components.Keys);
		}
		else {
			var mergedComps = new Dictionary<Type, object>(existing.Components);
			foreach (var kv in entry.Components) {
				log.DetectConflict(entry.Key, kv.Key, source);
				mergedComps[kv.Key] = kv.Value;
			}
			graph.MutableEntries[entry.Key] = new DataEntry(entry.Key, mergedComps) { SourceFile = entry.SourceFile };
			log.RecordWrite(entry.Key, source, entry.Components.Keys);
		}
	}

	private static void ApplyFieldPatch(DataEntry entry, DataSource source, int priority, DataGraph graph, ConflictLog log) {
		if (!graph.Entries.TryGetValue(entry.Key, out var existing)) {
			graph.MutableEntries[entry.Key] = entry;
			log.RecordWrite(entry.Key, source, entry.Components.Keys);
			return;
		}

		// FieldPatch: Incoming non-default fields win, existing fills default fields.
		// TODO: In a future version, generate shadow tracking structs to distinguish explicit defaults from uninitialized defaults.
		var mergedComps = new Dictionary<Type, object>(existing.Components);
		foreach (var kv in entry.Components) {
			if (existing.Components.TryGetValue(kv.Key, out var existingComp)) {
				log.DetectConflict(entry.Key, kv.Key, source);
				var merged = ComponentMerger.Merge(kv.Key, current: kv.Value, inherited: existingComp);
				mergedComps[kv.Key] = merged;
			}
			else {
				mergedComps[kv.Key] = kv.Value;
			}
		}

		graph.MutableEntries[entry.Key] = new DataEntry(entry.Key, mergedComps) { SourceFile = entry.SourceFile };
		log.RecordWrite(entry.Key, source, entry.Components.Keys);
	}

	private static void ApplyReplace(DataEntry entry, DataSource source, DataGraph graph, ConflictLog log) {
		if (graph.Entries.ContainsKey(entry.Key)) {
			log.DetectReplacement(entry.Key, source);
		}
		graph.MutableEntries[entry.Key] = entry;
		log.RecordWrite(entry.Key, source, entry.Components.Keys);
	}

	private static int GetExistingPriority(DataEntry entry) {
		if (entry.TryGet<Layer>(out var l)) {
			return l.Value;
		}
		return 0;
	}
}
