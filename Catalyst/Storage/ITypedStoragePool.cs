namespace Catalyst.Storage;

using System;

public interface ITypedStoragePool {
    int Count { get; }
    void Resize(int size);
    ref readonly T Get<T>(int index) where T : struct;
    void Set<T>(int index, T value) where T : struct;
}
