namespace Catalyst.Storage;

public interface ITypedStoragePool {
	public int Count { get; }
	public void Resize(int size);
	public T Get<T>(int index) where T : struct;
	public void Set<T>(int index, T value) where T : struct;
}
