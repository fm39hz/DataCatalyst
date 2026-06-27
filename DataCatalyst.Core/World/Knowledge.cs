namespace DataCatalyst.Knowledge;

using System;
using System.Collections.Generic;
using DataCatalyst.Schema;
using DataCatalyst.Storage;

public sealed class Knowledge {
	internal readonly IReadOnlyDictionary<Type, ITypedStoragePool> Pools;
	internal readonly IReadOnlyDictionary<Type, int> BeingIndices;
	internal readonly SchemaRegistry? Schema;
	private readonly BakedCache _bakedCache;
	private readonly DynamicAccess _dynamicAccess = new();
	internal IReadOnlyDictionary<Type, ITypedStoragePool> BeingPools { get; private set; } = new Dictionary<Type, ITypedStoragePool>();

	internal Knowledge(Dictionary<Type, ITypedStoragePool> pools, Dictionary<Type, int> beingIndices,
		SchemaRegistry? schema, Dictionary<Type, Dictionary<string, object>> bakedCache) {
		Pools = new Dictionary<Type, ITypedStoragePool>(pools);
		BeingIndices = new Dictionary<Type, int>(beingIndices);
		Schema = schema;
		_bakedCache = new BakedCache(bakedCache);
	}

	internal void SetDynamicPools(Dictionary<string, IStoragePool> pools, Dictionary<string, int> indices)
		=> _dynamicAccess.SetPools(pools, indices);

	internal void SetBeingPools(Dictionary<Type, ITypedStoragePool> pools) => BeingPools = pools;

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
		if (!Pools.TryGetValue(conceptType, out var pool))
			throw new InvalidOperationException($"No data for concept '{conceptType.Name}'");
		return pool.Get<TAspect>(RequireBeingIndex(beingType));
	}

	public TAspect About<TAspect>(Type beingType)
		where TAspect : struct {
		var idx = RequireBeingIndex(beingType);

		if (BeingPools.TryGetValue(beingType, out var pool)) {
			return pool.Get<TAspect>(idx);
		}

		foreach (var p in Pools.Values) {
			if (TryGetFromPool<TAspect>(p, idx, out var result)) {
				return result;
			}
		}
		throw new InvalidOperationException($"Aspect '{typeof(TAspect).Name}' not found for '{beingType.Name}'");
	}

	private static bool TryGetFromPool<TAspect>(ITypedStoragePool pool, int index, out TAspect result) where TAspect : struct {
		if (index < 0 || index >= pool.Count) {
			result = default;
			return false;
		}
		// Get<T> throws KeyNotFoundException when the aspect type is not in this pool.
		// This is expected during cross-pool scan in About<T>. Guard with try-catch
		// instead of requiring every consumer to check HasAspect first.
		try { result = pool.Get<TAspect>(index); return true; }
		catch (KeyNotFoundException) { result = default; return false; }
	}

	public int GetBeingIndex(Type beingType)
		=> BeingIndices.TryGetValue(beingType, out var idx) ? idx : -1;

	public int GetBeingIndex<TBeing>() where TBeing : struct, IBeing
		=> GetBeingIndex(typeof(TBeing));

	public ITypedStoragePool? GetPool(Type conceptType)
		=> Pools.TryGetValue(conceptType, out var pool) ? pool : null;

	public IRawStoragePool? GetDynamicPool(string conceptName) => _dynamicAccess.GetPool(conceptName);
	public int GetDynamicBeingIndex(string beingKey) => _dynamicAccess.GetIndex(beingKey);
	public IEnumerable<string> GetDynamicConceptNames() => _dynamicAccess.GetConceptNames();

	public TBaked GetBaked<TBaked>(string beingKey) => _bakedCache.Get<TBaked>(beingKey);
	public TBaked GetBaked<TBaked, TBeing>() where TBeing : struct, IBeing
		=> GetBaked<TBaked>(typeof(TBeing).Name);
	public IReadOnlyDictionary<string, TBaked> GetBaked<TBaked>() => _bakedCache.GetAll<TBaked>();
	public bool TryGetBaked<TBaked>(string beingKey, out TBaked result) => _bakedCache.TryGet(beingKey, out result);
	public bool TryGetBaked<TBaked, TBeing>(out TBaked result) where TBeing : struct, IBeing
		=> TryGetBaked(typeof(TBeing).Name, out result);
}
