namespace DataCatalyst.Abstractions;

public readonly struct DataRef<T> where T : struct {
    public string Key { get; }
    public DataRef(string key) => Key = key;
    public bool HasValue => Key is not null;
    public override string ToString() => Key ?? "";
}
