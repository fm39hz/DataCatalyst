namespace DataCatalyst.Plugins.GameConcept;

using System.Collections.Generic;
using DataCatalyst.Core;

/// <summary>
/// Type-safe scoped access to entries within a concept domain.
/// TConcept is a phantom type that provides compile-time scoping (e.g. ItemConcept, EnemyConcept).
/// </summary>
	public sealed class ConceptCatalog<TConcept> where TConcept : struct {
	internal ConceptCatalog(IReadOnlyDictionary<string, DataEntry> entries, string conceptName) {
		Entries = entries;
		ConceptName = conceptName;
	}

	/// <summary>The concept name (e.g., "Item", "Enemy").</summary>
	public string ConceptName { get; }

	/// <summary>All entries in this concept domain.</summary>
	public IReadOnlyDictionary<string, DataEntry> Entries { get; }

	/// <summary>Gets a component by entry key, scoped to this concept.</summary>
	public TComponent Get<TComponent>(string key) where TComponent : struct {
		if (Entries.TryGetValue(key, out var entry)) {
			return entry.Get<TComponent>();
		}
		throw new KeyNotFoundException(
			$"Entry '{key}' not found in concept '{ConceptName}'");
	}

	/// <summary>Attempts to get a component by entry key.</summary>
	public bool TryGet<TComponent>(string key, out TComponent value)
		where TComponent : struct {
		if (Entries.TryGetValue(key, out var entry) && entry.TryGet(out value)) {
			return true;
		}
		value = default;
		return false;
	}

	/// <summary>Checks if a key exists in this concept.</summary>
	public bool ContainsKey(string key) => Entries.ContainsKey(key);

	/// <summary>Number of entries in this concept.</summary>
	public int Count => Entries.Count;
}
