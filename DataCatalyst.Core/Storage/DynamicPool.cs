using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DataCatalyst.Storage;

internal sealed class DynamicPool : IStoragePool
{
    readonly List<Dictionary<Type, object>> _typedRow = new();
    readonly List<Dictionary<int, object?>> _rawRow = new();

    public int Count => Math.Max(_typedRow.Count, _rawRow.Count);

    public void Resize(int size)
    {
        while (_typedRow.Count < size) _typedRow.Add(new());
        while (_rawRow.Count < size) _rawRow.Add(new());
    }

    public void Set<T>(int index, T value) where T : struct
    { if (index >= 0 && index < _typedRow.Count) _typedRow[index][typeof(T)] = value; }

    public void SetRaw(int index, Type type, object value)
    { if (index >= 0 && index < _typedRow.Count) _typedRow[index][type] = value; }

    public void SetRawValue(int index, int aspectId, object? value)
    { if (index >= 0 && index < _rawRow.Count) _rawRow[index][aspectId] = value; }

    public ref readonly T Get<T>(int index) where T : struct
    {
        if (index < 0 || index >= _typedRow.Count)
            throw new IndexOutOfRangeException();
        if (_typedRow[index].TryGetValue(typeof(T), out var val))
            return ref Unsafe.Unbox<T>(val);
        return ref Unsafe.NullRef<T>(); // fallback; caller should check
    }

    public object? GetRaw(int index, int aspectId)
    {
        if (index < 0 || index >= _rawRow.Count) return null;
        return _rawRow[index].TryGetValue(aspectId, out var val) ? val : null;
    }
}
