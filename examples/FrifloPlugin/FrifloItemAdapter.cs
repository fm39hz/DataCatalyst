// Bridge: DataCatalyst Item → Friflo Entity.
// Stores only ItemKind - actual data lives in DataCatalyst's FrozenDictionary.
// Register: DataViewAdapterRegistry.Register<Item>(new FrifloItemAdapter(store));

using FM39hz.DataCatalyst.Runtime;
using Friflo.Engine.ECS;

public sealed class FrifloItemAdapter(EntityStore store) : IDataViewAdapter<Item>
{
	private readonly EntityStore _store = store;
	private readonly Dictionary<ItemKind, Entity> _map = new();

	public void OnEntryAdded(string key, Item entry)
	{
		var e = _store.CreateEntity();
		e.Add(new ItemRef { Kind = entry.Kind });
		e.Name = key;
		_map[entry.Kind] = e;
	}

	public void OnEntryRemoved(string key)
	{
		if (Item.TryGetKind(key, out var kind) && _map.Remove(kind, out var e))
			e.Delete();
	}

	public void OnEntryModified(string key, Item old, Item @new)
	{
		// Data already updated in DataCatalyst - no components to mutate
	}

	public void OnAllCleared()
	{
		foreach (var (_, e) in _map) e.Delete();
		_map.Clear();
	}
}

// Lightweight ECS component
public struct ItemRef : IComponent
{
	public ItemKind Kind;
}
