namespace Catalyst.Storage;

using System;
using System.Collections.Generic;

internal sealed class DynamicPool : IStoragePool {
	private readonly List<Dictionary<Type, object>> _typedRow = [];
	private readonly List<Dictionary<int, object?>> _rawRow = [];

	public int Count => Math.Max(_typedRow.Count, _rawRow.Count);

	public void Resize(int size) {
		while (_typedRow.Count < size) {
			_typedRow.Add([]);
		}

		while (_rawRow.Count < size) {
			_rawRow.Add([]);
		}
	}

	public void Set<T>(int index, T value) where T : struct {
		if (index >= 0 && index < _typedRow.Count) {
			_typedRow[index][typeof(T)] = value;
		}
	}

	public void SetRaw(int index, Type type, object value) {
		if (index >= 0 && index < _typedRow.Count) {
			_typedRow[index][type] = value;
		}
	}

	public void SetRawValue(int index, int aspectId, object? value) {
		if (index >= 0 && index < _rawRow.Count) {
			_rawRow[index][aspectId] = value;
		}
	}

	public T Get<T>(int index) where T : struct {
		if (index < 0 || index >= _typedRow.Count) {
			throw new ArgumentOutOfRangeException(nameof(index));
		}

		if (_typedRow[index].TryGetValue(typeof(T), out var val)) {
			return (T)val;
		}

		throw new KeyNotFoundException($"Aspect '{typeof(T).Name}' not found at index {index}");
	}

	public object? GetRaw(int index, int aspectId) {
		if (index < 0 || index >= _rawRow.Count) {
			return null;
		}

		return _rawRow[index].TryGetValue(aspectId, out var val) ? val : null;
	}
}
