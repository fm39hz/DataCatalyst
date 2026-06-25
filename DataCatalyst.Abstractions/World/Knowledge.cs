namespace DataCatalyst.Knowledge;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using DataCatalyst.Schema;
using DataCatalyst.Storage;

public sealed class Knowledge {
	internal readonly FrozenDictionary<Type, IStoragePool> Pools;
	internal readonly FrozenDictionary<Type, int> BeingIndices;
	internal readonly SchemaRegistry? Schema;

	internal Knowledge(Dictionary<Type, IStoragePool> pools, Dictionary<Type, int> beingIndices,
		SchemaRegistry? schema = null) {
		Pools = pools.ToFrozenDictionary();
		BeingIndices = beingIndices.ToFrozenDictionary();
		Schema = schema;
	}

	public ConceptScope<TConcept> Of<TConcept>()
		where TConcept : struct, IConcept {
		if (!Pools.TryGetValue(typeof(TConcept), out var pool)) {
			throw new InvalidOperationException(
				$"Concept '{typeof(TConcept).Name}' has no data in this world");
		}

		return new ConceptScope<TConcept>(this);
	}

	public int GetBeingIndex(Type beingType)
		=> BeingIndices.TryGetValue(beingType, out var idx) ? idx : -1;
}
