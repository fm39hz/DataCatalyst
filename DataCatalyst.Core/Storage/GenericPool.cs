using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DataCatalyst.Storage;

internal sealed class GenericPool : IStoragePool
{
    readonly List<Dictionary<Type, object>> _rows = new();

    public int Count => _rows.Count;

    public void Resize(int size)
    {
        while (_rows.Count < size)
            _rows.Add(new Dictionary<Type, object>());
    }

    public void SetRaw(int index, Type type, object value)
    {
        if (index >= 0 && index < _rows.Count)
            _rows[index][type] = value;
    }

    public ref readonly T Get<T>(int index) where T : struct
    {
        if (index < 0 || index >= _rows.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return ref Unsafe.Unbox<T>(_rows[index][typeof(T)]);
    }

    public void Set<T>(int index, T value) where T : struct
    {
        if (index >= 0 && index < _rows.Count)
            _rows[index][typeof(T)] = value;
    }
}
