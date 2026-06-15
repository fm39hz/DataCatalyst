// Scripting VM bridge - exposes DataCatalyst + game engine to Lua mods.
// Modders write .lua files; game loads them at runtime. No rebuild needed.

using FM39hz.DataCatalyst.Runtime;
using MoonSharp.Interpreter;

public sealed class ScriptBridge {
    private readonly Script _lua = new();

    public ScriptBridge(EntityStore store, SystemRoot root) {
        // Expose DataCatalyst data API
        _lua.Globals["Data"] = new DataBridge();

        // Expose game engine API
        _lua.Globals["ECS"] = new EcsBridge(store, root);
    }

    public void LoadModScripts(string dir) {
        foreach (var file in Directory.EnumerateFiles(dir, "*.lua"))
            _lua.DoFile(file);
    }
}

// DataCatalyst bridge - script reads/writes data through generated contracts
public sealed class DataBridge {
    public void Add(string catalog, string key, Table entry) {
        switch (catalog) {
            case "Item":
                ItemMod.AddEntry(key, new Item {
                    Health = (int)(entry["Health"]?.Number ?? 0),
                    Weight = (float)(entry["Weight"]?.Number ?? 0),
                });
                break;
            case "Buff":
                BuffMod.AddEntry(key, new Buff {
                    Power = (int)(entry["Power"]?.Number ?? 0),
                });
                break;
        }
    }

    public Table Get(string catalog, string kind) {
        if (catalog == "Item" && Item.TryGetKind(kind, out var k)) {
            var item = Item.Get(k);
            var t = new Table();
            t["Health"] = item.Health;
            t["Weight"] = item.Weight;
            return t;
        }
        return null;
    }
}

// ECS bridge - script registers systems and spawns entities
public sealed class EcsBridge {
    private readonly EntityStore _store;
    private readonly SystemRoot _root;
    private readonly Dictionary<string, ScriptSystem> _systems = new();

    public EcsBridge(EntityStore store, SystemRoot root) {
        _store = store;
        _root = root;
    }

    public int CreateEntity() => _store.CreateEntity().Id;

    public void AddComponent(int entityId, string compType, Table data) {
        var e = _store.GetEntityById(entityId);
        switch (compType) {
            case "Health": e.Add(new Health((int)(data["Value"]?.Number ?? 0))); break;
            case "Position": e.Add(new Position(
                (float)(data["X"]?.Number ?? 0),
                (float)(data["Y"]?.Number ?? 0)
            )); break;
        }
    }

    public void RegisterSystem(string name, Table def) {
        if (_systems.ContainsKey(name)) return;
        var sys = new ScriptSystem(name, def, _store);
        _systems[name] = sys;
        _root.Add(sys);
    }
}

// Friflo ECS system that delegates to Lua callback each tick
public sealed class ScriptSystem : QuerySystem {
    private readonly string _name;
    private readonly Table _def;
    private readonly EntityStore _store;

    public ScriptSystem(string name, Table def, EntityStore store) {
        _name = name;
        _def = def;
        _store = store;
    }

    protected override void OnUpdate() {
        var entities = new List<Table>();
        foreach (var e in _store.Entities) {
            var t = new Table();
            if (_def["components"] is Table comps) {
                foreach (var comp in comps.Values) {
                    var name = comp.String;
                    // copy component data into table for script
                }
            }
            entities.Add(t);
        }

        var args = new object[] { Tick.deltaTime, entities };
        _def["run"]?.Function?.Call(args);
    }
}
