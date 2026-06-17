namespace DataCatalyst.Abstractions;

using System.Collections.Generic;

/// <summary>Generic repository for typed data access.</summary>
public interface IDataRepository<TKey, TValue> {
	/// <summary>Returns the value for the given key.</summary>
	public TValue Get(TKey key);
	/// <summary>Attempts to retrieve the value for the given key.</summary>
	public bool TryGet(TKey key, out TValue value);
	/// <summary>Returns all stored values.</summary>
	public IEnumerable<TValue> GetAll();
	/// <summary>Total number of entries.</summary>
	public int Count { get; }
}
