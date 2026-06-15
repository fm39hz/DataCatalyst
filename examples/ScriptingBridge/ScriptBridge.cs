// Scripting VM bridge - exposes DataCatalyst + game engine to Lua mods.
// Typed methods for known catalogs (generated pattern); entity IDs for ECS access.

using FM39hz.DataCatalyst.Runtime;
using MoonSharp.Interpreter;

public sealed class ScriptBridge
{
	private readonly Script _lua = new();

	public ScriptBridge(EntityStore store, SystemRoot root)
	{
		// Typed data methods - no string catalog lookup
		_lua.Globals["Data_AddItem"] = (Action<string, int, float>)((key, health, weight) =>
			ItemMod.AddEntry(key, new Item { Health = health, Weight = weight }));

		_lua.Globals["Data_AddBuff"] = (Action<string, int>)((key, power) =>
			BuffMod.AddEntry(key, new Buff { Power = power }));

		_lua.Globals["Data_GetItem"] = (Func<string, Item>)((kind) =>
			Item.TryGetKind(kind, out var k) ? Item.Get(k) : default);

		// ECS - entity ID, not Table
		_lua.Globals["ECS"] = new EcsBridge(store, root);
	}

	public void LoadModScripts(string dir)
	{
		foreach (var file in Directory.EnumerateFiles(dir, "*.lua"))
			_lua.DoFile(file);
	}
}

// ECS bridge - entity ID + typed component accessors
public sealed class EcsBridge(EntityStore store, SystemRoot root)
{
	private readonly EntityStore _store = store;
	private readonly SystemRoot _root = root;

	public int CreateEntity() => _store.CreateEntity().Id;

	public int AddItemEntity(string kind, int id)
	{
		if (!Item.TryGetKind(kind, out var k)) return id;
		var e = _store.GetEntityById(id);
		e.Add(new ItemRef { Kind = k });
		return id;
	}

	public void RegisterSystem(string name, Table def)
	{
		var sys = new ScriptSystem(name, def, _store);
		_root.Add(sys);
	}
}

// Friflo system - passes entity ID to Lua, no Table allocation per entity
public sealed class ScriptSystem(string name, Table def, EntityStore store) : QuerySystem
{
	private readonly string _name = name;
	private readonly Table _def = def;
	private readonly EntityStore _store = store;

	protected override void OnUpdate()
	{
		var fn = _def["run"]?.Function;
		if (fn is null) return;

		var dt = Tick.deltaTime;
		foreach (var e in _store.Entities)
			fn.Call(e.Id, dt);     // int, float - no allocations
	}
}

// Minimal component for ECS → DataCatalyst link
public struct ItemRef : IComponent { public ItemKind Kind; }
