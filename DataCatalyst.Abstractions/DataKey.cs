namespace DataCatalyst.Abstractions;

public readonly struct DataKey<T> where T : struct {
    public string Id { get; }
    public DataKey(string id) => Id = id;
    public bool HasValue => Id is not null;
    public override string ToString() => Id ?? "";
    public bool Equals(DataKey<T> other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is DataKey<T> other && Equals(other);
    public override int GetHashCode() => Id?.GetHashCode() ?? 0;
    public static bool operator ==(DataKey<T> left, DataKey<T> right) => left.Equals(right);
    public static bool operator !=(DataKey<T> left, DataKey<T> right) => !left.Equals(right);
}
