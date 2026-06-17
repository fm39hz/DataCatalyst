namespace DataCatalyst.Core;

using System.Collections.Generic;

/// <summary>Resolved catalog of data entries.</summary>
public sealed class DataCatalog {
	private readonly Dictionary<string, DataEntry> _entries;

	internal DataCatalog(Dictionary<string, DataEntry> entries) {
		_entries = entries;
	}

	/// <summary>All entries in the catalog.</summary>
	public IReadOnlyDictionary<string, DataEntry> Entries => _entries;

	/// <summary>Gets a component by entry key.</summary>
	public T Get<T>(string key) where T : struct {
		if (_entries.TryGetValue(key, out var entry)) {
			return entry.Get<T>();
		}

		throw new KeyNotFoundException($"Entry '{key}' not found");
	}

	/// <summary>Attempts to get a component by entry key.</summary>
	public bool TryGet<T>(string key, out T value) where T : struct {
		if (_entries.TryGetValue(key, out var entry) && entry.TryGet(out value)) {
			return true;
		}

		value = default;
		return false;
	}

	/// <summary>Checks if a key exists in the catalog.</summary>
	public bool ContainsKey(string key) => _entries.ContainsKey(key);
}
