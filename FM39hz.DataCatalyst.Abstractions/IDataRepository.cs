namespace FM39hz.DataCatalyst.Abstractions;

using System.Collections.Generic;

public interface IDataRepository<TKey, TValue> {
	TValue Get(TKey key);
	bool TryGet(TKey key, out TValue value);
	IEnumerable<TValue> GetAll();
	int Count { get; }
}
