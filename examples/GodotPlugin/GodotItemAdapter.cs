// Bridge: DataCatalyst Item → Godot Node + metadata
// Register once at startup:
//   DataViewAdapterRegistry.Register<Item>(new GodotItemAdapter(GetTree().Root));

using FM39hz.DataCatalyst.Runtime;
using Godot;

public sealed class GodotItemAdapter : IDataViewAdapter<Item> {
    private readonly Node _parent;
    private readonly Dictionary<ItemKind, Node> _map = new();

    public GodotItemAdapter(Node parent) => _parent = parent;

    public void OnEntryAdded(string key, Item entry) {
        var node = new Node { Name = key };
        node.SetMeta("health", entry.Health);
        node.SetMeta("weight", entry.Weight);
        _parent.AddChild(node);
        if (Item.TryGetKind(key, out var kind))
            _map[kind] = node;
    }

    public void OnEntryRemoved(string key) {
        if (Item.TryGetKind(key, out var kind) && _map.Remove(kind, out var node))
            node.QueueFree();
    }

    public void OnEntryModified(string key, Item old, Item @new) {
        if (!Item.TryGetKind(key, out var kind) || !_map.TryGetValue(kind, out var node)) return;
        node.SetMeta("health", @new.Health);
        node.SetMeta("weight", @new.Weight);
    }

    public void OnAllCleared() {
        foreach (var (_, node) in _map) node.QueueFree();
        _map.Clear();
    }
}
