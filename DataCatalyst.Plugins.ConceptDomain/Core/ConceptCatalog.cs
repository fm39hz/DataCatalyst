namespace DataCatalyst.Plugins.ConceptDomain;

using System;
using System.Collections.Generic;
using DataCatalyst.Core;

/// <summary>
/// Type-safe scoped access to entries within a concept domain.
/// TTag is a phantom type that provides compile-time scoping.
/// </summary>
public sealed class ConceptCatalog<TTag> where TTag : struct {
	private readonly IReadOnlyDictionary<string, DataEntry> _entries;

	internal ConceptCatalog(IReadOnlyDictionary<string, DataEntry> entries) {
		_entries = entries;
	}

	/// <summary>The concept name (e.g., "Item", "Enemy").</summary>
	public string ConceptName { get; internal set; } = "";

	/// <summary>All entries in this concept domain.</summary>
	public IReadOnlyDictionary<string, DataEntry> Entries => _entries;

	/// <summary>Gets a component by entry key, scoped to this concept.</summary>
	public TComponent Get<TComponent>(string key) where TComponent : struct {
		if (_entries.TryGetValue(key, out var entry)) {
			return entry.Get<TComponent>();
		}
		throw new KeyNotFoundException(
			$"Entry '{key}' not found in concept '{ConceptName}'");
	}

	/// <summary>Attempts to get a component by entry key.</summary>
	public bool TryGet<TComponent>(string key, out TComponent value)
		where TComponent : struct {
		if (_entries.TryGetValue(key, out var entry) && entry.TryGet(out value)) {
			return true;
		}
		value = default;
		return false;
	}

	/// <summary>Checks if a key exists in this concept.</summary>
	public bool ContainsKey(string key) => _entries.ContainsKey(key);

	/// <summary>Number of entries in this concept.</summary>
	public int Count => _entries.Count;
}
