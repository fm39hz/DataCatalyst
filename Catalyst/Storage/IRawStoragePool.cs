namespace Catalyst.Storage;

using System;

public interface IRawStoragePool {
    int Count { get; }
    void Resize(int size);
    void SetRaw(int index, Type type, object value);
    void SetRawValue(int index, int aspectId, object? value);
    object? GetRaw(int index, int aspectId);
}
