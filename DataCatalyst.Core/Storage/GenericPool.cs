using System;
using System.Collections.Generic;

namespace DataCatalyst.Storage;

/// <summary>Fallback pool when SourceGen hasn't generated a typed pool.
/// Components are pre-deserialized by ResolveCrossRefStage.</summary>
internal sealed class GenericPool : IStoragePool
{
    private readonly List<Dictionary<Type, object>> _rows = new();

    public GenericPool() { }

    public int Count => _rows.Count;

    public void Resize(int size)
    {
        while (_rows.Count < size)
            _rows.Add(new Dictionary<Type, object>());
    }

    public void SetRaw(int index, Type type, object value)
    {
        if (index < 0 || index >= _rows.Count) return;
        _rows[index][type] = value;
    }

    public T Get<T>(int index) where T : struct
    {
        if (index < 0 || index >= _rows.Count) throw new IndexOutOfRangeException();
        if (_rows[index].TryGetValue(typeof(T), out var val)) return (T)val;
        return default;
    }

    public void Set<T>(int index, T value) where T : struct
    {
        if (index < 0 || index >= _rows.Count) throw new IndexOutOfRangeException();
        _rows[index][typeof(T)] = value;
    }
}
