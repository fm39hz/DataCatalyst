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
	private readonly FrozenDictionary<Type, FrozenDictionary<string, object>> _bakedCache;

	internal Knowledge(Dictionary<Type, IStoragePool> pools, Dictionary<Type, int> beingIndices,
		SchemaRegistry? schema, Dictionary<Type, Dictionary<string, object>> bakedCache) {
		Pools = pools.ToFrozenDictionary();
		BeingIndices = beingIndices.ToFrozenDictionary();
		Schema = schema;

		var frozenCache = new Dictionary<Type, FrozenDictionary<string, object>>();
		foreach (var kv in bakedCache) {
			frozenCache[kv.Key] = kv.Value.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
		}
		_bakedCache = frozenCache.ToFrozenDictionary();
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

	/// <summary>
	/// Gets the index of a Being type in the world.
	/// </summary>
	public int GetBeingIndex<TBeing>() where TBeing : struct, IBeing
		=> GetBeingIndex(typeof(TBeing));

	public IStoragePool? GetPool(Type conceptType)
		=> Pools.TryGetValue(conceptType, out var pool) ? pool : null;

	/// <summary>
	/// Gets a baked data structure of type TBaked for a specific Being key.
	/// </summary>
	public TBaked GetBaked<TBaked>(string beingKey) {
		if (_bakedCache.TryGetValue(typeof(TBaked), out var inner) && inner.TryGetValue(beingKey, out var obj)) {
			return (TBaked)obj;
		}
		throw new KeyNotFoundException($"No baked data of type '{typeof(TBaked).Name}' found for being '{beingKey}'");
	}

	/// <summary>
	/// Gets a baked data structure of type TBaked for a specific Being type.
	/// </summary>
	public TBaked GetBaked<TBaked, TBeing>() where TBeing : struct, IBeing
		=> GetBaked<TBaked>(typeof(TBeing).Name);

	/// <summary>
	/// Gets all baked data structures of type TBaked, mapped by Being key.
	/// </summary>
	public IReadOnlyDictionary<string, TBaked> GetBaked<TBaked>() {
		if (_bakedCache.TryGetValue(typeof(TBaked), out var inner)) {
			var result = new Dictionary<string, TBaked>(StringComparer.OrdinalIgnoreCase);
			foreach (var kv in inner) {
				result[kv.Key] = (TBaked)kv.Value;
			}
			return result;
		}
		return FrozenDictionary<string, TBaked>.Empty;
	}

	/// <summary>
	/// Tries to get a baked data structure of type TBaked for a specific Being key.
	/// </summary>
	public bool TryGetBaked<TBaked>(string beingKey, out TBaked result) {
		if (_bakedCache.TryGetValue(typeof(TBaked), out var inner) && inner.TryGetValue(beingKey, out var obj)) {
			result = (TBaked)obj;
			return true;
		}
		result = default!;
		return false;
	}

	/// <summary>
	/// Tries to get a baked data structure of type TBaked for a specific Being type.
	/// </summary>
	public bool TryGetBaked<TBaked, TBeing>(out TBaked result) where TBeing : struct, IBeing
		=> TryGetBaked<TBaked>(typeof(TBeing).Name, out result);
}
