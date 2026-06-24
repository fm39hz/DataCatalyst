namespace DataCatalyst.Storage;

public interface IStoragePool
{
    int Count { get; }
    void Resize(int size);
    T Get<T>(int index) where T : struct;
    void Set<T>(int index, T value) where T : struct;
    void SetRaw(int index, System.Type type, object value);
}
