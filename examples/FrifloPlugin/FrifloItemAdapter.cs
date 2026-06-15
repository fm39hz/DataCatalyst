// Bridge: DataCatalyst Item → Friflo Entity + components
// Register once at startup:
//   DataViewAdapterRegistry.Register<Item>(new FrifloItemAdapter(store));

using FM39hz.DataCatalyst.Runtime;
using Friflo.Engine.ECS;

public sealed class FrifloItemAdapter : IDataViewAdapter<Item> {
    private readonly EntityStore _store;
    private readonly Dictionary<ItemKind, Entity> _map = new();

    public FrifloItemAdapter(EntityStore store) => _store = store;

    public void OnEntryAdded(string key, Item entry) {
        var e = _store.CreateEntity();
        e.Add(new ItemWeight   { Value = entry.Weight });
        e.Add(new ItemHealth   { Value = entry.Health });
        e.Add(new ItemKindRef  { Kind  = entry.Kind });
        if (Item.TryGetKind(key, out var kind))
            _map[kind] = e;
    }

    public void OnEntryRemoved(string key) {
        if (Item.TryGetKind(key, out var kind) && _map.Remove(kind, out var e))
            e.Delete();
    }

    public void OnEntryModified(string key, Item old, Item @new) {
        if (!Item.TryGetKind(key, out var kind) || !_map.TryGetValue(kind, out var e)) return;
        e.Set(new ItemWeight { Value = @new.Weight });
        e.Set(new ItemHealth { Value = @new.Health });
    }

    public void OnAllCleared() {
        foreach (var (_, e) in _map) e.Delete();
        _map.Clear();
    }
}
