namespace DataCatalyst.Knowledge;

using System;
using System.Collections.Generic;
using DataCatalyst.Schema;
using DataCatalyst.Storage;

public sealed class Knowledge {
	internal readonly IReadOnlyDictionary<Type, IStoragePool> Pools;
	internal readonly IReadOnlyDictionary<Type, int> BeingIndices;
	internal readonly SchemaRegistry? Schema;
	private readonly IReadOnlyDictionary<Type, IReadOnlyDictionary<string, object>> _bakedCache;
	private IReadOnlyDictionary<string, IStoragePool> _dynamicPools = new Dictionary<string, IStoragePool>();
	private IReadOnlyDictionary<string, int> _dynamicIndices = new Dictionary<string, int>();

	internal Knowledge(Dictionary<Type, IStoragePool> pools, Dictionary<Type, int> beingIndices,
		SchemaRegistry? schema, Dictionary<Type, Dictionary<string, object>> bakedCache) {
		Pools = pools;
		BeingIndices = beingIndices;
		Schema = schema;

		var cache = new Dictionary<Type, IReadOnlyDictionary<string, object>>();
		foreach (var kv in bakedCache) {
			cache[kv.Key] = kv.Value;
		}
		_bakedCache = cache;
	}

	internal void SetDynamicPools(Dictionary<string, IStoragePool> pools, Dictionary<string, int> indices) {
		_dynamicPools = pools;
		_dynamicIndices = indices;
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

	public int GetBeingIndex<TBeing>() where TBeing : struct, IBeing
		=> GetBeingIndex(typeof(TBeing));

	public IStoragePool? GetPool(Type conceptType)
		=> Pools.TryGetValue(conceptType, out var pool) ? pool : null;

	public IStoragePool? GetDynamicPool(string conceptName)
		=> _dynamicPools.TryGetValue(conceptName, out var pool) ? pool : null;

	public int GetDynamicBeingIndex(string beingKey)
		=> _dynamicIndices.TryGetValue(beingKey, out var idx) ? idx : -1;

	public IEnumerable<string> GetDynamicConceptNames() => _dynamicPools.Keys;

	public TBaked GetBaked<TBaked>(string beingKey) {
		if (_bakedCache.TryGetValue(typeof(TBaked), out var inner) && inner.TryGetValue(beingKey, out var obj))
			return (TBaked)obj;
		throw new KeyNotFoundException($"No baked data of type '{typeof(TBaked).Name}' found for being '{beingKey}'");
	}

	public TBaked GetBaked<TBaked, TBeing>() where TBeing : struct, IBeing
		=> GetBaked<TBaked>(typeof(TBeing).Name);

	public IReadOnlyDictionary<string, TBaked> GetBaked<TBaked>() {
		if (_bakedCache.TryGetValue(typeof(TBaked), out var inner)) {
			var result = new Dictionary<string, TBaked>(StringComparer.OrdinalIgnoreCase);
			foreach (var kv in inner)
				result[kv.Key] = (TBaked)kv.Value;
			return result;
		}
		return new Dictionary<string, TBaked>();
	}

	public bool TryGetBaked<TBaked>(string beingKey, out TBaked result) {
		if (_bakedCache.TryGetValue(typeof(TBaked), out var inner) && inner.TryGetValue(beingKey, out var obj)) {
			result = (TBaked)obj;
			return true;
		}
		result = default!;
		return false;
	}

	public bool TryGetBaked<TBaked, TBeing>(out TBaked result) where TBeing : struct, IBeing
		=> TryGetBaked(typeof(TBeing).Name, out result);
}
