# DataCatalyst

Roslyn source generator: **JSON → strongly-typed C# struct + enum + FrozenDictionary** at compile time. Zero reflection. NativeAOT-safe. Engine-agnostic.

```
dotnet add package FM39hz.DataCatalyst --version 0.2.0
```

---

## Example

```csharp
[CatalystData("Items.json")]
public partial struct Item { }

var hp = Item.Potion.Health;
var item = Item.Get(ItemKind.Elixir);
var heavy = Item.Query(x => x.Weight > 1f);
Item.TryGetKind("Potion", out var kind);
```

## Backend switching

```csharp
[CatalystData("Items.json", Backend = DataBackendConst.All)]
public partial struct Item { }
```

**SQLite** — `IDbConnection`/`DbDataReader`, no hard dependency on Microsoft.Data.Sqlite:

```csharp
var repo = new ItemSqlRepository(() => new SqliteConnection("..."));
ItemSql.CreateSelectAllCommand(conn);
ItemSql.ReadRow((DbDataReader)reader);
```

**JSON** — `Utf8JsonReader`, no reflection:

```csharp
ItemJson.Read(ref reader);
ItemJson.LoadAll("items.json");
```

**Env-based**: `DATACATALYST_BACKEND`, `DATACATALYST_CONNECTION`, `DATACATALYST_DATAPATH`.

## Mod content — runtime, drop-in files

```csharp
[CatalystData("Items.json", ModSupport = true)]
public partial struct Item { }

ItemMod.LoadMods("mods/items/");           // JSON files at runtime
ItemMod.AddEntry("SuperPotion", new() { Health = 999 });
ItemMod.RemoveEntry("OldPotion");

var item = Item.Get(ItemKind.Potion);      // mod override → core
```

Mod JSON format:
```json
[
  { "Kind": "IronSword", "Damage": 25, "Weight": 3 },
  { "Kind": "Elixir",    "Damage": 0,  "Weight": 0.5 }
]
```

**Engine adapter** — one-time bridge per engine, notified on changes:

```csharp
public class EcsItemAdapter : IDataViewAdapter<Item> {
    public void OnEntryAdded(string key, Item entry) {
        var e = World.Create();
        e.Add(new Health(entry.Health));
    }
    public void OnEntryRemoved(string key) { }
    public void OnAllCleared() { }
}
```

**DSL reader** — custom text format:

```csharp
public sealed class YamlItemReader : IDslReader<Item> {
    public string FileExtension => ".yaml";
    public bool TryRead(string text, out Item value) { /* parse */ }
}
```

## Steam Workshop / Nexus — game startup

```csharp
public override void _Ready() {
    // Workshop syncs files into these dirs automatically
    ItemMod.LoadMods("Mods/Items");              // data mods
    ItemMod.LoadMods("Mods/Buffs");

    DataViewAdapterRegistry.Register<Item>(       // bridge to engine
        new FrifloItemAdapter(_store));

    PluginRegistry.LoadAll(new ModGameContext());  // code mods (build-merged)
}
```

**User**: download mod → launcher syncs files → play. No rebuild, no tools.  
**Developer**: 3 lines + 1 adapter per catalog.

## Code mod — scripting layer (runtime code)

For runtime code mods, add a scripting VM on top of DataCatalyst's data API:

```csharp
// Game exposes DataCatalyst + engine to script
[ModPlugin("ScriptEngine")]
public sealed class ScriptEngine : IModPlugin {
    public void OnLoad(IModGameContext ctx) {
        ItemMod.AddEntry("LuaSword", new Item { Weight = 2f, Health = 60 });
    }
}

// Lua mod example (via MoonSharp):
//   Data.Add("Item", "FireSword", { Weight = 3, Health = 80 })
//   ECS.RegisterSystem("BurnAura", { run = function(dt, e) end })

public sealed class ScriptBridge {
    private readonly ScriptEngine _lua;

    public ScriptBridge() {
        _lua = new ScriptEngine();
        _lua.Globals["Data"] = new DataBridge();
        _lua.Globals["ECS"]  = new EcsBridge(store);
    }

    public void LoadModScripts(string dir) {
        foreach (var file in Directory.EnumerateFiles(dir, "*.lua"))
            _lua.DoFile(file);
    }
}

public sealed class DataBridge {
    public void Add(string catalog, string key, object entry) {
        if (catalog == "Item")
            ItemMod.AddEntry(key, (Item)entry);
    }
}
```

**DataCatalyst handles data; scripting VM handles logic.** The game wires them together.

## Code mod — development-time only

NativeAOT cannot load assemblies at runtime. Code mods must be compiled into the binary. This is a **NativeAOT constraint**, not a DataCatalyst design choice.

For development or first-party mods, an MSBuild target is available:

```xml
<PropertyGroup>
  <EnableModMerge>true</EnableModMerge>
</PropertyGroup>
```

Mods directory convention — each subdirectory with `mod.json` is scanned:
```
Mods/
├── SkillPack/
│   ├── mod.json
│   ├── Skills.cs                          ← compiled
│   └── skills_override.json               ← AdditionalFiles
└── AuraSystem/
    ├── mod.json
    └── AuraSystem.cs
```

Plugins register via `[ModPlugin]`:

```csharp
[ModPlugin("SkillPack")]
public class SkillPack : IModPlugin {
    public void OnLoad(IModGameContext ctx) {
        ItemMod.AddEntry("SuperPotion", new() { Health = 500 });
        ctx.GetService<ISystemRegistry>()?.Register(new CombatLevelSystem());
    }
}
```

Build:
```
dotnet publish -p:EnableModMerge=true
```

> **Trade-offs:** compile-time baking means zero runtime cost, but all entries are memory-resident. For massive datasets where lazy loading matters, complement DataCatalyst with a runtime loader. Scripting (Lua, C# scripting) is the game's responsibility — DataCatalyst only handles data.

## Cross-catalog references

Declare dependencies with `RefTo` — DataCatalyst topo-sorts catalogs so referenced data is available when needed.

```csharp
public struct BuffTemplate {
    public int Power { get; init; }
}

public struct ItemTemplate {
    public int Damage { get; init; }
    public DataRef<Buff, BuffKind> BuffRef { get; init; }
}

[CatalystData("buffs.json", typeof(BuffTemplate))]
public partial struct Buff { }

[CatalystData("items.json", typeof(ItemTemplate), RefTo = new[] { typeof(Buff) })]
public partial struct Item { }

// Item.BuffRef stores BuffKind enum — resolved via Buff.Get()
var buff = Buff.Get(item.BuffRef.Kind);
```

## Generated API

| Member | Description |
|---|---|---|
| `Item.Potion` | Static field per JSON entry |
| `Item.Get(ItemKind)` | O(1) FrozenDictionary lookup |
| `Item.Query(predicate)` | Filter entries |
| `Item.TryGetKind(string, out ItemKind)` | Deterministic enum mapping |
| `Item.Repository { get; set; }` | Swappable IDataRepository |
| `ItemMod.AddEntry(key, value)` | Mod data injection |
| `ItemSql.CreateSelectAllCommand(conn)` | SQLite IDbCommand factory |
| `DataRef<Buff, BuffKind>` | Typed reference to another catalog |
| `CatalogRegistry.GetAll()` | Lists all discovered catalogs |

All catalogs auto-register into `CatalogRegistry` via `[ModuleInitializer]`.

## Generated components

| Backend | ModSupport | Files |
|---|---|---|
| `None` | `false` | Core struct + enum + FrozenDictionary |
| `Json` | `false` | Core + `ItemJson` + `ItemJsonRepository` |
| `Sqlite` | `false` | Core + `ItemSql` + `ItemSqlRepository` |
| `All` | `false` | Core + JSON + SQLite |
| any | `true` | Core + JSON + `ItemMod` + adapter notifications |

## Examples

| Example | Shows |
|---|---|
| [`FrifloPlugin/FrifloItemAdapter.cs`](examples/FrifloPlugin/FrifloItemAdapter.cs) | Bridge data entries → Friflo Entity + components |
| [`FrifloPlugin/FrifloModPlugin.cs`](examples/FrifloPlugin/FrifloModPlugin.cs) | Mod plugin registering Friflo systems via `IModPlugin` |
| [`GodotPlugin/GodotItemAdapter.cs`](examples/GodotPlugin/GodotItemAdapter.cs) | Bridge data entries → Godot Node + metadata |
| [`GodotPlugin/GodotModPlugin.cs`](examples/GodotPlugin/GodotModPlugin.cs) | Mod plugin spawning Godot nodes + processing |

## Extension points

| Extension | Interface | Registry |
|---|---|---|
| Engine adapter | `IDataViewAdapter<T>` | `DataViewAdapterRegistry` |
| DSL reader | `IDslReader<T>` | `DslReaderRegistry` |
| C# mod plugin | `IModPlugin` | `PluginRegistry` |
| Custom backend | `IDataRepository<TKey, TValue>` | `Item.Repository = ...` |
| Generator plugin | `IPrimitiveTypeRule`, `ITypeEmitter` | `DcPluginRegistry` |

## License

MIT
