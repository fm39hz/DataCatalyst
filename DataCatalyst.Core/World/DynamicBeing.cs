namespace DataCatalyst.Modding;

using System;
using System.Collections.Generic;
using DataCatalyst.Storage;

public readonly ref struct DynamicConcept {
	private readonly global::DataCatalyst.Knowledge.Knowledge _knowledge;
	private readonly IStoragePool? _pool;
	private readonly string _conceptName;

	internal DynamicConcept(global::DataCatalyst.Knowledge.Knowledge knowledge, string conceptName, IStoragePool? pool) {
		_knowledge = knowledge;
		_conceptName = conceptName;
		_pool = pool;
	}

	public DynamicBeing At(string beingKey) {
		if (_pool != null) {
			var idx = _knowledge.GetDynamicBeingIndex(beingKey);
			if (idx >= 0) return new DynamicBeing(_pool, idx, beingKey);
		}
		return DynamicBeing.Empty;
	}

	public bool Has(string beingKey) => _knowledge.GetDynamicBeingIndex(beingKey) >= 0;
}

public readonly ref struct DynamicBeing {
	private readonly IStoragePool? _pool;
	private readonly int _index;
	private readonly string _beingKey;
	private readonly bool _valid;

	public static DynamicBeing Empty => default;

	internal DynamicBeing(IStoragePool pool, int index, string beingKey) {
		_pool = pool;
		_index = index;
		_beingKey = beingKey;
		_valid = true;
	}

	public string Key => _beingKey;
	public bool IsValid => _valid;
	public int Index => _index;

	public T GetField<T>(string aspectName, string fieldName) where T : struct {
		if (_pool == null || !_valid) throw new InvalidOperationException("Being not found");
		return default;
	}

	public bool TryGetField<T>(string aspectName, string fieldName, out T value) where T : struct {
		value = default;
		if (_pool == null || !_valid) return false;
		return false;
	}

	public object? GetRaw(string aspectName) {
		if (_pool == null || !_valid) return null;
		return null;
	}
}

public static class KnowledgeDynamicExtensions {
	public static DynamicConcept Of(this global::DataCatalyst.Knowledge.Knowledge knowledge, string conceptName) {
		var pool = knowledge.GetDynamicPool(conceptName);
		return new DynamicConcept(knowledge, conceptName, pool);
	}

	public static bool HasConcept(this global::DataCatalyst.Knowledge.Knowledge knowledge, string conceptName) {
		return knowledge.GetDynamicPool(conceptName) != null;
	}
}
