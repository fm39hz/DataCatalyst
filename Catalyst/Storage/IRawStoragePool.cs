namespace Catalyst.Storage;

using System;

public interface IRawStoragePool {
	public int Count { get; }
	public void Resize(int size);
	public void SetRaw(int index, Type type, object value);
	public void SetRawValue(int index, int aspectId, object? value);
	public object? GetRaw(int index, int aspectId);
}
