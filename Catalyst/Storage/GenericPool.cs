namespace Catalyst.Storage;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

internal sealed class GenericPool : ITypedStoragePool {
	private readonly List<Dictionary<Type, object>> _rows = [];

	public int Count => _rows.Count;

	public void Resize(int size) {
		while (_rows.Count < size) {
			_rows.Add([]);
		}
	}

	public ref readonly T Get<T>(int index) where T : struct {
		if (index < 0 || index >= _rows.Count) {
			throw new ArgumentOutOfRangeException(nameof(index));
		}

		if (_rows[index].TryGetValue(typeof(T), out var val)) {
			return ref Unsafe.Unbox<T>(val);
		}

		throw new KeyNotFoundException($"Aspect '{typeof(T).Name}' not found at index {index}");
	}

	public void Set<T>(int index, T value) where T : struct {
		if (index >= 0 && index < _rows.Count) {
			_rows[index][typeof(T)] = value;
		}
	}

	public void SetRaw(int index, Type type, object value) {
		if (index >= 0 && index < _rows.Count) {
			_rows[index][type] = value;
		}
	}

	public void SetRawValue(int index, int aspectId, object? value) { }
	public object? GetRaw(int index, int aspectId) => null;
}
