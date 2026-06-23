namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Abstractions;

/// <summary>Resolved catalog of data entries. Supports both int (fast) and string (runtime) key access.</summary>
public sealed class DataCatalog {
	private readonly Dictionary<string, DataEntry> _entries;
	private readonly Dictionary<string, int> _keyToId;
	private readonly List<DataEntry?> _byId;

	internal DataCatalog(Dictionary<string, DataEntry> entries) {
		_entries = entries;

		// Build int-indexed list sorted alphabetically (matching source-gen entry order)
		var sorted = entries.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
		_keyToId = new Dictionary<string, int>(sorted.Count, StringComparer.Ordinal);
		_byId = new List<DataEntry?>(sorted.Count);

		for (var i = 0; i < sorted.Count; i++) {
			_keyToId[sorted[i]] = i;
			_byId.Add(entries[sorted[i]]);
		}
	}

	/// <summary>All entries in the catalog.</summary>
	public IReadOnlyDictionary<string, DataEntry> Entries => _entries;

	/// <summary>Gets a component by int entry id (fast path — O(1), no hash).</summary>
	public T Get<T>(int entryId) where T : struct {
		if (entryId < 0 || entryId >= _byId.Count || _byId[entryId] == null)
			throw new KeyNotFoundException($"Entry id '{entryId}' not found");
		return _byId[entryId]!.Get<T>();
	}

	/// <summary>Attempts to get a component by int entry id.</summary>
	public bool TryGet<T>(int entryId, out T value) where T : struct {
		if (entryId >= 0 && entryId < _byId.Count && _byId[entryId] != null) {
			return _byId[entryId]!.TryGet(out value);
		}
		value = default;
		return false;
	}

	/// <summary>Checks if an entry id exists in the catalog.</summary>
	public bool ContainsKey(int entryId) => entryId >= 0 && entryId < _byId.Count && _byId[entryId] != null;

	/// <summary>Gets the DataEntry for the given entry id.</summary>
	public DataEntry GetEntry(int entryId) =>
		_byId[entryId] ?? throw new KeyNotFoundException($"Entry id '{entryId}' not found");

	/// <summary>Resolves a string entry key to its int entry id. Returns -1 if not found.</summary>
	internal int GetEntryId(string key) => _keyToId.TryGetValue(key, out var id) ? id : -1;

	/// <summary>Updates a resolved entry in-place (internal use only, e.g. overlay application).</summary>
	internal void UpdateEntry(string key, DataEntry newEntry) {
		_entries[key] = newEntry;
		if (_keyToId.TryGetValue(key, out var id)) {
			_byId[id] = newEntry;
		}
	}
}
