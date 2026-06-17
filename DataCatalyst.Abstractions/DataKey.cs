namespace DataCatalyst.Abstractions;

/// <summary>Typed cross-reference by string key.</summary>
public readonly record struct DataKey<T>(string Id) where T : struct {
	/// <summary>Whether the key has been assigned an identifier.</summary>
	public bool HasValue => Id is not null;
	/// <summary>Returns the identifier string, or empty.</summary>
	public override string ToString() => Id ?? "";
}
