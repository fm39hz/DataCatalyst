namespace Catalyst.Knowledge;

using System;
using System.Collections.Generic;
using System.Linq;
using Catalyst;
using Catalyst.Schema;
using Catalyst.Storage;

public sealed class Knowledge {
	internal readonly IReadOnlyDictionary<Type, ITypedStoragePool> Pools;
	internal readonly IReadOnlyDictionary<Type, int> BeingIndices;
	internal readonly SchemaRegistry? Schema;
	private readonly DynamicAccess _dynamicAccess = new();
	internal FlatStore? _flatStore;

	internal Knowledge(Dictionary<Type, ITypedStoragePool> pools, Dictionary<Type, int> beingIndices,
		SchemaRegistry? schema) {
		Pools = new Dictionary<Type, ITypedStoragePool>(pools);
		BeingIndices = new Dictionary<Type, int>(beingIndices);
		Schema = schema;
	}

	internal void SetDynamicPools(Dictionary<string, IStoragePool> pools, Dictionary<string, int> indices)
		=> _dynamicAccess.SetPools(pools, indices);

	private int RequireBeingIndex(Type beingType) {
		var idx = GetBeingIndex(beingType);
		if (idx < 0) throw new InvalidOperationException($"Being '{beingType.Name}' not found");
		return idx;
	}

	public TAspect Of<TConcept, TAspect>(Type beingType)
		where TConcept : struct, IConcept
		where TAspect : struct, IRevealedBy<TConcept> {
		return Get<TAspect>(typeof(TConcept), beingType);
	}

	public TAspect Get<TAspect>(Type conceptType, Type beingType)
		where TAspect : struct {
		var idx = RequireBeingIndex(beingType);
		if (_flatStore != null && _flatStore.TryGet<TAspect>(out var arr))
			return arr[idx];
		if (!Pools.TryGetValue(conceptType, out var pool))
			throw new InvalidOperationException($"No data for concept '{conceptType.Name}'");
		return pool.Get<TAspect>(idx);
	}

	public TAspect About<TAspect>(Type beingType)
		where TAspect : struct {
		if (typeof(TAspect).GetInterfaces().Any(i => i.IsGenericType
			&& i.GetGenericTypeDefinition() == typeof(IRevealedBy<>)))
			throw new InvalidOperationException(
				$"'{typeof(TAspect).Name}' is revealed by a concept. Use Of<TConcept, TAspect>() instead of About<T>().");

		var idx = RequireBeingIndex(beingType);
		if (_flatStore != null && _flatStore.TryGet<TAspect>(out var arr) && idx < arr.Length)
			return arr[idx];
		throw new InvalidOperationException($"Free aspect '{typeof(TAspect).Name}' not found for '{beingType.Name}'");
	}

	internal int GetBeingIndex(Type beingType)
		=> BeingIndices.TryGetValue(beingType, out var idx) ? idx : -1;

	internal int GetBeingIndex<TBeing>() where TBeing : struct, IBeing
		=> GetBeingIndex(typeof(TBeing));

	internal ITypedStoragePool? GetPool(Type conceptType)
		=> Pools.TryGetValue(conceptType, out var pool) ? pool : null;

	public IRawStoragePool? GetDynamicPool(string conceptName) => _dynamicAccess.GetPool(conceptName);
	public int GetDynamicBeingIndex(string beingKey) => _dynamicAccess.GetIndex(beingKey);
	public IEnumerable<string> GetDynamicConceptNames() => _dynamicAccess.GetConceptNames();
}
