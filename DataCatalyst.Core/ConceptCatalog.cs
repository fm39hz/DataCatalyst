using DataCatalyst.Abstractions;

namespace DataCatalyst.Core;

using System.Collections.Generic;
using DataCatalyst.Core;

/// <summary>
/// Type-safe scoped access to entries within a concept domain.
/// TConcept is a phantom type that provides compile-time scoping (e.g. ItemConcept, EnemyConcept).
/// </summary>
public sealed class ConceptCatalog<TConcept> where TConcept : struct {
	private readonly Dictionary<int, DataEntry> _entriesById;

	internal ConceptCatalog(IReadOnlyDictionary<int, DataEntry> entries, string conceptName) {
		_entriesById = new Dictionary<int, DataEntry>(entries);
		ConceptName = conceptName;
	}

	/// <summary>The concept name (e.g., "Item", "Enemy").</summary>
	public string ConceptName { get; }

	/// <summary>Gets a component by int entry id, scoped to this concept.</summary>
	public TComponent Get<TComponent>(int entryId) where TComponent : struct {
		if (_entriesById.TryGetValue(entryId, out var entry)) {
			return entry.Get<TComponent>();
		}
		throw new KeyNotFoundException(
			$"Entry id '{entryId}' not found in concept '{ConceptName}'");
	}

	/// <summary>Attempts to get a component by int entry id.</summary>
	public bool TryGet<TComponent>(int entryId, out TComponent value)
		where TComponent : struct {
		if (_entriesById.TryGetValue(entryId, out var entry) && entry.TryGet(out value)) {
			return true;
		}
		value = default;
		return false;
	}

	/// <summary>Checks if an entry id exists in this concept.</summary>
	public bool ContainsKey(int entryId) => _entriesById.ContainsKey(entryId);

	/// <summary>Number of entries in this concept.</summary>
	public int Count => _entriesById.Count;
}
