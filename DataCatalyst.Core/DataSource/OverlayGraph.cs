namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Abstractions;

/// <summary>
/// Graph containing orthogonal data overlays (like translations or debug overrides)
/// that are applied after the main catalog is built and plugins have run.
/// </summary>
public sealed class OverlayGraph {
	private readonly Dictionary<string, List<OverlayEntryRecord>> _overlays = new(StringComparer.Ordinal);

	/// <summary>
	/// Adds an overlay entry associated with a data source.
	/// </summary>
	public void Add(DataEntry entry, DataSource source) {
		if (!_overlays.TryGetValue(entry.Key, out var list)) {
			list = new List<OverlayEntryRecord>();
			_overlays[entry.Key] = list;
		}
		list.Add(new OverlayEntryRecord(entry, source));
	}

	/// <summary>
	/// Applies all registered overlays to a resolved catalog.
	/// </summary>
	public void ApplyTo(DataCatalog catalog) {
		foreach (var kvp in _overlays) {
			var entryKey = kvp.Key;
			var records = kvp.Value;

			var id = catalog.GetEntryId(entryKey);
			if (id == -1) continue; // Base entry does not exist in catalog, overlay is ignored

			var baseEntry = catalog.GetEntry(id);
			var mergedComps = new Dictionary<Type, object>(baseEntry.Components);

			// Sort overlays by source priority (higher priority overlay runs last and overrides lower)
			var sortedRecords = records.OrderBy(r => r.Source.Priority).ToList();

			foreach (var record in sortedRecords) {
				foreach (var kv in record.Entry.Components) {
					if (mergedComps.TryGetValue(kv.Key, out var existingComp)) {
						// Apply field-level merging using ComponentMerger
						var merged = ComponentMerger.Merge(kv.Key, current: kv.Value, inherited: existingComp);
						mergedComps[kv.Key] = merged;
					}
					else {
						mergedComps[kv.Key] = kv.Value;
					}
				}
			}

			var newEntry = new DataEntry(entryKey, mergedComps) { SourceFile = baseEntry.SourceFile };
			catalog.UpdateEntry(entryKey, newEntry);
		}
	}

	private sealed class OverlayEntryRecord {
		public DataEntry Entry { get; }
		public DataSource Source { get; }

		public OverlayEntryRecord(DataEntry entry, DataSource source) {
			Entry = entry;
			Source = source;
		}
	}
}
