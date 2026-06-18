namespace DataCatalyst.Core;

using System.Collections.Concurrent;

internal sealed class RegistryStore<TKey, TValue> where TKey : notnull {
	private readonly ConcurrentDictionary<TKey, TValue> _items = [];

	public void Add(TKey key, TValue value) => _items[key] = value;

	public bool TryGet(TKey key, out TValue? value) => _items.TryGetValue(key, out value);

	public TValue[] GetAll() => [.. _items.Values];

	public void Remove(TKey key) => _items.TryRemove(key, out _);

	public void Clear() => _items.Clear();
}
