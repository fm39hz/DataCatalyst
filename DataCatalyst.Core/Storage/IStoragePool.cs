namespace DataCatalyst.Storage;

using System;

public interface IStoragePool {
	public int Count { get; }
	public void Resize(int size);
	public ref readonly T Get<T>(int index) where T : struct;
	public void Set<T>(int index, T value) where T : struct;
	public void SetRaw(int index, Type type, object value);
}
