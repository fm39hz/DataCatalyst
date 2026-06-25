using System;

namespace DataCatalyst.Storage;

public interface IStoragePool
{
    int Count { get; }
    void Resize(int size);
    ref readonly T Get<T>(int index) where T : struct;
    void Set<T>(int index, T value) where T : struct;
    void SetRaw(int index, Type type, object value);
}
