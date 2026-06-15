# FM39hz.DataCatalyst

Roslyn source generator: **JSON → strong-typed C# struct + enum + FrozenDictionary** at compile time.
Zero reflection. NativeAOT-safe. Engine-agnostic.

**Opinionated philosophy:** data-driven definitions are compile-time materialized; runtime flexibility (backend switching, mod loading, C# plugins) goes through generated contracts — never through reflection or runtime JSON parsing for core data.

---

## Quick Start

### 1. Setup

```xml
<ItemGroup>
  <AdditionalFiles Include="Data\*.json" />
</ItemGroup>
```

### 2. Define

```csharp
using FM39hz.DataCatalyst;

[CatalystData("Items.json")]
public partial struct Item { }
```

### 3. Use

```csharp
// Compile-time baked — zero overhead
var hp = Item.Potion.Health;                       // static field
var item = Item.Get(ItemKind.Elixir);               // O(1) FrozenDictionary

// Query/filter
var heavy = Item.Query(x => x.Weight > 1f);

// Enum mapping (deterministic, for external data)
if (Item.TryGetKind("Potion", out var kind)) { ... }
```

---

## Attribute Reference

```csharp
[CatalystData(
    string jsonPath,                        // path in <AdditionalFiles>
    string entryPoint = "",                 // JSON sub-property ("" = root)
    Type templateType = null,               // CLR type to constrain schema
    string KeyField = "",                   // required for array-of-objects
    int Backend = 0,                        // DataBackendConst.*
    bool ModSupport = false                 // enable mod overlay
)]

[ModPlugin(
    string name,                            // unique plugin identifier
    string[] dependencies = null            // dependency names
)]
```

**Backend constants** (`DataBackendConst`):
| Constant | Value | Meaning |
|---|---|---|
| `None` | 0 | Core only (default) |
| `Json` | 1 | Core + JSON runtime reader |
| `Sqlite` | 2 | Core + SQLite reader |
| `All` | 3 | Core + JSON + SQLite |

---

## Generated API — Core Struct

Given `[CatalystData("Items.json")] partial struct Item { }` with JSON:

```json
{
	"Potion": { "Health": 50, "Weight": 0.5 },
	"Elixir": { "Health": 200, "Weight": 0.3 }
}
```

```csharp
public enum ItemKind { Potion, Elixir }

public partial struct Item {
    // Properties
    public int Health { get; init; }
    public float Weight { get; init; }
    public ItemKind Kind { get; init; }

    // Static fields — compile-time baked
    public static readonly Item Potion = new() { Kind = ItemKind.Potion, Health = 50, Weight = 0.5 };
    public static readonly Item Elixir = new() { Kind = ItemKind.Elixir, Health = 200, Weight = 0.3 };

    // FrozenDictionary — O(1) lookup
    public static FrozenDictionary<ItemKind, Item> All;
    public static FrozenDictionary<string, ItemKind> KindByName;

    // Properties
    public static int Count => All.Count;
    public static IEnumerable<ItemKind> Kinds => All.Keys;
    public static IEnumerable<Item> Values => All.Values;

    // Access — type-safe, mod-aware, backend-aware
    public static Item Get(ItemKind kind);
    public static bool TryGet(ItemKind kind, out Item value);
    public static bool Contains(ItemKind kind);

    // Query/Filter
    public static IEnumerable<Item> Query(Func<Item, bool> predicate);
    public static IEnumerable<Item> Find(Func<Item, bool> predicate);

    // String→Kind mapping (deterministic)
    public static ItemKind GetKind(string name);
    public static bool TryGetKind(string name, out ItemKind kind);

    // Repository — game can swap at startup
    public static IDataRepository<ItemKind, Item> Repository { get; set; }
}
```

**When `Backend != None`**:

```csharp
    private sealed class CoreItemRepository : IDataRepository<ItemKind, Item>;
    public static IDataRepository<ItemKind, Item> ResolveRepository();
```

**When `ModSupport = true`**: `Get`/`TryGet` automatically check mod entries before core data.

---

## Mod Content (Data Files)

Enable with `ModSupport = true`. Mod data files at runtime override or extend core data.

### JSON mod files

Mod directory structure:

```
mods/items/
├── buff_potions.json     ← entries with Kind matching enum → override
├── new_items.json        ← entries with known Kind → override
└── config_mod.json       ← DSL reader plugin
```

Mod JSON format (array of objects, each with `"Kind"`):

```json
[
	{ "Kind": "Potion", "Health": 100, "Weight": 0.4 },
	{ "Kind": "Elixir", "Health": 500, "Weight": 0.3 }
]
```

### C# injection

```csharp
ItemMod.LoadMods("mods/items/");           // runtime file loading
ItemMod.AddEntry("CustomSword", new Item { Health = 999 });  // direct C#
ItemMod.AddRange(("Dagger", new Item { Health = 10 }), ...);  // bulk
ItemMod.RemoveEntry("OldPotion");          // remove
ItemMod.Clear();                           // reset

// Access — same API
var item = Item.Get(ItemKind.Potion);      // mod override → core
var allMods = ItemMod.GetAllModEntries();  // enumerate all mod entries
```

### Engine adapter — automatic notification

```csharp
// Write once per engine — receives callbacks on every AddEntry/RemoveEntry/Clear
public class FrifloItemAdapter : IDataViewAdapter<Item> {
    public void OnEntryAdded(string key, Item entry) => _store.CreateEntity().Add(...);
    public void OnEntryRemoved(string key) { ... }
    public void OnEntryModified(string key, Item old, Item @new) { ... }
    public void OnAllCleared() { ... }
}

[ModuleInitializer]
internal static void RegisterAdapter() =>
    DataViewAdapterRegistry.Register<Item>(new FrifloItemAdapter(store));
```

### DSL reader — custom text format

```csharp
public sealed class YamlItemReader : IDslReader<Item> {
    [ModuleInitializer]
    internal static void Register() => DslReaderRegistry.Register(new YamlItemReader());
    public string FileExtension => ".yaml";
    public bool TryRead(string text, out Item value) { ... }
}
```

---

## Runtime Backend Switching

Enable with `Backend = DataBackendConst.Json` / `Sqlite` / `All`.

### SQLite backend

```csharp
DataBackendSelector.Initialize("sqlite");  // hoặc để env var DATACATALYST_BACKEND

// Auto-resolve
var item = Item.Get(ItemKind.Potion);  // reads from SQLite repo

// Or manual
using var conn = new SqliteConnection("Data Source=game.db");
var cmd = ItemSql.CreateSelectAllCommand(conn);  // IDbCommand
conn.Open();
using var reader = cmd.ExecuteReader();
while (reader.Read()) {
    var item = ItemSql.ReadRow((DbDataReader)reader);  // ordinal access
}
var repo = new ItemSqlRepository(() => new SqliteConnection("..."));  // lazy load
```

**No Microsoft.Data.Sqlite hard dependency**: uses `IDbConnection`/`DbDataReader` (BCL).  
**Lazy loading**: data loaded on first `Get()`, thread-safe.

### JSON backend

```csharp
var item = ItemJson.Read(ref reader);         // Utf8JsonReader, no reflection
var items = ItemJson.LoadAll("items.json");    // List<Item>
var repo = new ItemJsonRepository("items.json");
```

**Flat schema required** (no nested objects/arrays). DC0015/DC0016 if violated.

### Environment variables

| Variable                  | Used by               | Values                   |
| ------------------------- | --------------------- | ------------------------ |
| `DATACATALYST_BACKEND`    | `DataBackendSelector` | `json`, `sqlite`, `all`  |
| `DATACATALYST_CONNECTION` | `ResolveRepository()` | SQLite connection string |
| `DATACATALYST_DATAPATH`   | `ResolveRepository()` | JSON file path           |

---

## C# Mod Plugins

Mod plugins are compiled together with the game (NativeAOT-safe). No `Assembly.Load`, no `Activator.CreateInstance`.

### Define a plugin

```csharp
[ModPlugin("SkillPack", ["CoreLib"])]
public class SkillPack : IModPlugin {
    public string Name => "SkillPack";
    public string[] Dependencies => ["CoreLib"];

    public void OnLoad(IModGameContext ctx) {
        // Data injection
        ItemMod.AddEntry("SuperPotion", new Item { Health = 500 });

        // Engine system — via service registry
        var systems = ctx.GetService<ISystemRegistry>();
        systems?.Register(new CombatLevelSystem());
    }
}
```

### Game startup

```csharp
// 1. Register engine-specific services
var ctx = new ModGameContext();
ctx.RegisterService<ISystemRegistry>(new FrifloSystemRegistry(store, simulation));

// 2. Load all plugins (topo-sorted by dependencies)
PluginRegistry.LoadAll(ctx);
```

**Generated automatically** by `[ModPlugin]` scanner:

```csharp
[ModuleInitializer]
internal static void Register_SkillPack() => PluginRegistry.Register(new SkillPack());
```

---

## Cross-Catalog Data References

```csharp
// Runtime struct (emitted in DataCatalystRuntime.g.cs)
public readonly struct DataRef<TTarget, TTargetKind> where TTargetKind : struct {
    public TTargetKind Kind { get; }
    public DataRef(TTargetKind kind) => Kind = kind;
}
```

Use in template types for typed catalogs:

```csharp
public struct ItemTemplate {
    public int Health { get; init; }
    public DataRef<Buff, BuffKind> BuffRef { get; init; }
}

[CatalystData("items.json", "items", typeof(ItemTemplate))]
public partial struct Item { }

// Item.BuffRef is DataRef<Buff, BuffKind>
// Game layer resolves:
var buff = Buff.Get(item.BuffRef.Kind);
```

---

## Runtime Catalog Discovery

All catalogs auto-register at startup:

```csharp
[ModuleInitializer]
internal static void RegisterCatalog() => CatalogRegistry.Register<Item>();

// Enumerate at runtime:
foreach (var type in CatalogRegistry.GetAll()) {
    Console.WriteLine(type.Name);  // "Item"
}
```

---

## Engine Binding — `IDataViewAdapter<T>`

Write once per engine, receive callbacks when mod data changes:

| Engine                  | Adapter                                      |
| ----------------------- | -------------------------------------------- |
| **Friflo ECS**          | Create Entity + add IComponent               |
| **Arch ECS**            | Create Entity + add component (handle stale) |
| **Unity Entities**      | Create Entity + add IComponentData           |
| **Unity MonoBehaviour** | Instantiate GameObject + add component       |
| **Godot Node**          | Create Node + set metadata                   |
| **MonoGame**            | Update game state dictionary                 |

```csharp
public class GodotItemAdapter : IDataViewAdapter<Item> {
    Node _parent;
    Dictionary<ItemKind, Node> _map;

    public void OnEntryAdded(string key, Item entry) {
        var node = new Node { Name = key };
        node.SetMeta("health", entry.Health);
        _parent.AddChild(node);
        _map[entry.Kind] = node;
    }
    // ... OnEntryRemoved, OnEntryModified, OnAllCleared
}
```

---

## Generated Components Matrix

| Backend      | `ModSupport` | Generated files                                                 |
| ------------ | ------------ | --------------------------------------------------------------- |
| `None` (0)   | `false`      | Core struct + enum + FrozenDictionary                           |
| `Json` (1)   | `false`      | Core + `ItemJson` (Utf8JsonReader) + `ItemJsonRepository`       |
| `Sqlite` (2) | `false`      | Core + `ItemSql` (SQL) + `ItemSqlRepository` (lazy)             |
| `All` (3)    | `false`      | Core + JSON + SQLite                                            |
| any          | `true`       | Core + JSON + `ItemMod` (overlay + DSL + adapter notifications) |
| `All`        | `true`       | Core + JSON + SQLite + Mod                                      |

**Always emitted** (once per compilation): `DataCatalystRuntime.g.cs` + `CatalystDataAttribute.g.cs`

---

## Design Properties

| Property                     | Detail                                                             |
| ---------------------------- | ------------------------------------------------------------------ |
| **No strings in public API** | All access through `ItemKind` enum                                 |
| **AOT-safe**                 | No `Assembly.Load`, no `Activator.CreateInstance`, no `Enum.Parse` |
| **Engine-agnostic**          | `IDataViewAdapter<T>` + `ServiceRegistry` — 1 adapter per engine   |
| **Swap backend**             | `Item.Repository = repo` or env `DATACATALYST_BACKEND`             |
| **Mod = data**               | Content only, no DLL loading at runtime                            |
| **Plugin = code**            | C# mod → compile together → NativeAOT                              |
| **Deterministic mapping**    | `TryGetKind(string, out Kind)` — only string→Kind converter        |
| **Zero overhead (core)**     | `FrozenDictionary`, static fields, compile-time baked              |
| **Lazy SQLite**              | `ItemSqlRepository(Func<IDbConnection>)` — loads on first access   |

---

## Extending DataCatalyst

| Extension point   | Interface                                 | Registry                  |
| ----------------- | ----------------------------------------- | ------------------------- |
| Engine adapter    | `IDataViewAdapter<T>`                     | `DataViewAdapterRegistry` |
| DSL reader format | `IDslReader<T>`                           | `DslReaderRegistry`       |
| C# mod plugin     | `IModPlugin`                              | `PluginRegistry`          |
| Custom backend    | `IDataRepository<TKey, TValue>`           | `Item.Repository = ...`   |
| Generator plugin  | `IPrimitiveTypeRule`, `ITypeEmitter`, ... | `DcPluginRegistry`        |

---

## License

MIT
