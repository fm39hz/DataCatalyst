namespace DataCatalyst.Modding;

using System;
using DataCatalyst.Storage;
using DataCatalyst.Schema;
using System.Collections.Generic;

public readonly ref struct DynamicConcept {
	private readonly Knowledge.Knowledge _knowledge;
	private readonly IRawStoragePool? _pool;
	private readonly string _conceptName;

	internal DynamicConcept(Knowledge.Knowledge knowledge, string conceptName, IRawStoragePool? pool) {
		_knowledge = knowledge;
		_conceptName = conceptName;
		_pool = pool;
	}

	public DynamicBeing At(string beingKey) {
		if (_pool != null) {
			var idx = _knowledge.GetDynamicBeingIndex(beingKey);
			if (idx >= 0) {
				return new DynamicBeing(_knowledge, _pool, idx, beingKey, _conceptName);
			}
		}
		return DynamicBeing.Empty;
	}

	public bool Has(string beingKey) => _knowledge.GetDynamicBeingIndex(beingKey) >= 0;
}

	public readonly ref struct DynamicBeing {
		private readonly IRawStoragePool? _pool;
		private readonly Knowledge.Knowledge? _knowledge;
		private readonly string _conceptName;

		public static DynamicBeing Empty => default;

		internal DynamicBeing(Knowledge.Knowledge knowledge, IRawStoragePool pool, int index, string beingKey, string conceptName) {
		_knowledge = knowledge;
		_pool = pool;
		_conceptName = conceptName;
		Index = index;
		Key = beingKey;
		IsValid = true;
	}

	public string Key { get; }
	public bool IsValid { get; }
	public int Index { get; }

	public T GetField<T>(string aspectName, string fieldName) where T : struct {
		if (TryGetField<T>(aspectName, fieldName, out var value)) {
			return value;
		}
		throw new KeyNotFoundException(
			$"Field '{fieldName}' not found in aspect '{aspectName}' for being '{Key}'");
	}

	public bool TryGetField<T>(string aspectName, string fieldName, out T value) where T : struct {
		value = default;
		if (_pool == null || _knowledge?.Schema == null) {
			return false;
		}

		var aspectId = _knowledge.Schema.GetAspectId(aspectName);
		if (!aspectId.HasValue) {
			return false;
		}

		var raw = _pool.GetRaw(Index, aspectId.Value);
			if (raw is System.Collections.Generic.Dictionary<string, object?> dict
				&& dict.TryGetValue(fieldName, out var val)
				&& val is T tVal) {
				value = tVal;
				return true;
			}
		return false;
	}

	public object? GetRaw(string aspectName) {
		if (_pool == null || _knowledge?.Schema == null) {
			return null;
		}

		var aspectId = _knowledge.Schema.GetAspectId(aspectName);
		if (!aspectId.HasValue) {
			return null;
		}

		return _pool.GetRaw(Index, aspectId.Value);
	}
}

public static class KnowledgeDynamicExtensions {
	public static DynamicConcept Of(this Knowledge.Knowledge knowledge, string conceptName) {
		var pool = knowledge.GetDynamicPool(conceptName);
		return new DynamicConcept(knowledge, conceptName, pool);
	}

	public static bool HasConcept(this Knowledge.Knowledge knowledge, string conceptName) => knowledge.GetDynamicPool(conceptName) != null;
}
