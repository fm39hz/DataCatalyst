// Bridge: DataCatalyst Item → Godot Node (C# subclass).
// Node stores only the enum key - actual data lives in DataCatalyst's FrozenDictionary.
// Register: DataViewAdapterRegistry.Register<Item>(new GodotItemAdapter(root));

using FM39hz.DataCatalyst.Runtime;
using Godot;

public sealed class GodotItemAdapter(Node parent) : IDataViewAdapter<Item>
{
	private readonly Node _parent = parent;
	private readonly Dictionary<ItemKind, Node> _map = new();

	public void OnEntryAdded(string key, Item entry)
	{
		var node = new ItemNode { Kind = entry.Kind, Name = key };
		_parent.AddChild(node);
		_map[entry.Kind] = node;
	}

	public void OnEntryRemoved(string key)
	{
		if (Item.TryGetKind(key, out var kind) && _map.Remove(kind, out var node))
			node.QueueFree();
	}

	public void OnEntryModified(string key, Item old, Item @new)
	{
		// Data already updated in DataCatalyst
	}

	public void OnAllCleared()
	{
		foreach (var (_, node) in _map) node.QueueFree();
		_map.Clear();
	}
}

// C# subclass of Node
public partial class ItemNode : Node
{
	public ItemKind Kind { get; set; }

	// Read current data directly from DataCatalyst
	public int Health => Item.Get(Kind).Health;
	public float Weight => Item.Get(Kind).Weight;
}
