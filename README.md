# DataCatalyst

Roslyn source generator: **JSON → strongly-typed C# struct + enum + FrozenDictionary** at compile time. Zero reflection. NativeAOT-safe. Engine-agnostic.

---

## Requirements

- .NET 8+ / .NET Standard 2.0+
- C# 12+

## Quick start

### Installation

```
dotnet add package FM39hz.DataCatalyst --version 0.2.5
```

### Project setup

```xml
<ItemGroup>
  <AdditionalFiles Include="Data\*.json" />
</ItemGroup>
```

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

**SQLite** — `IDbConnection`/`DbDataReader`, no hard dep:

```csharp
ItemSql.CreateSelectAllCommand(conn);
ItemSql.ReadRow((DbDataReader)reader);
var repo = new ItemSqlRepository(() => new SqliteConnection("..."));
```

**JSON** — `Utf8JsonReader`, no reflection:

```csharp
ItemJson.Read(ref reader);
ItemJson.LoadAll("items.json");
```

**Env**: `DATACATALYST_BACKEND`, `DATACATALYST_CONNECTION`, `DATACATALYST_DATAPATH`.

## Mod content — runtime, drop-in files

```csharp
[CatalystData("Items.json", ModSupport = true)]
public partial struct Item { }

ItemMod.LoadMods("mods/items/");           // JSON files at runtime
ItemMod.AddEntry("SuperPotion", new() { Health = 999 });
ItemMod.RemoveEntry("OldPotion");
var item = Item.Get(ItemKind.Potion);      // mod override → core
```

**Engine adapter** — notified on every change:

```csharp
public class EcsItemAdapter : IDataViewAdapter<Item> {
    public void OnEntryAdded(string key, Item entry) { /* create entity */ }
    public void OnEntryRemoved(string key) { /* destroy entity */ }
    public void OnAllCleared() { /* destroy all */ }
}
```

**DSL reader** — custom text format:

```csharp
public sealed class YamlItemReader : IDslReader<Item> {
    public string FileExtension => ".yaml";
    public bool TryRead(string text, out Item value) { /* parse */ }
}
```

## Steam Workshop / Nexus

```csharp
public override void _Ready() {
    ItemMod.LoadMods("Mods/Items");
    DataViewAdapterRegistry.Register<Item>(new MyAdapter(_store));
    PluginRegistry.LoadAll(new ModGameContext());
}
```

## Code mod — scripting layer

Add a Lua/C# scripting VM on top of DataCatalyst's data API:

```csharp
// Lua: Data.Add("Item", "FireSword", { Weight = 3, Health = 80 })
// Lua: ECS.RegisterSystem("BurnAura", { run = function(dt, e) end })

public sealed class ScriptBridge {
    Script _lua = new();
    public ScriptBridge() {
        _lua.Globals["Data"] = new DataBridge();
    }

    public void LoadModScripts(string dir) {
        foreach (var file in Directory.EnumerateFiles(dir, "*.lua"))
            _lua.DoFile(file);
    }
}
```

## Code mod — build merge (MSBuild)

```xml
<PropertyGroup><EnableModMerge>true</EnableModMerge></PropertyGroup>
```

```
Mods/
├── SkillPack/
│   ├── mod.json
│   ├── Skills.cs                          ← compiled
│   └── skills_override.json               ← AdditionalFiles
```

```csharp
[ModPlugin("SkillPack")]
public class SkillPack : IModPlugin {
    public void OnLoad(IModGameContext ctx) {
        ItemMod.AddEntry("SuperPotion", new() { Health = 500 });
        ctx.GetService<ISystemRegistry>()?.Register(new CombatLevelSystem());
    }
}
```

Build: `dotnet publish -p:EnableModMerge=true`

> **Trade-offs:** compile-time baking = zero runtime cost, memory-resident. Scripting (Lua, C#) is the game's responsibility — DataCatalyst handles data.

## Cross-catalog references

```csharp
[CatalystData("buffs.json", typeof(BuffTemplate))]
public partial struct Buff { }

[CatalystData("items.json", typeof(ItemTemplate), RefTo = new[] { typeof(Buff) })]
public partial struct Item { }

var buff = Buff.Get(item.BuffRef.Kind);
```

DataCatalyst topo-sorts by `RefTo` — referenced catalogs are processed first.

## Generated API

| Member                          | Description                                              |
| ------------------------------- | -------------------------------------------------------- |
| `Item.Potion`                   | Static field per JSON entry (Eager mode)                 |
| `Item.Get(ItemKind)`            | O(1) FrozenDictionary lookup (Eager) / Repository (Lazy) |
| `Item.Query(predicate)`         | Filter entries                                           |
| `Item.Repository { get; set; }` | Swappable IDataRepository                                |
| `ItemMod.AddEntry(key, value)`  | Mod data injection                                       |
| `DataRef<Buff, BuffKind>`       | Typed reference to another catalog                       |
| `CatalogRegistry.GetAll()`      | Lists all discovered catalogs                            |

All catalogs auto-register into `CatalogRegistry`.

## Load mode

```csharp
[CatalystData("Items.json")]                          // Lazy (default): Repository on first Get
[CatalystData("Items.json", LoadMode = LoadModeConst.Eager)]  // Eager: FrozenDictionary at module init
```

| Mode        | Static fields | FrozenDictionary | Startup cost | When to use                   |
| ----------- | ------------- | ---------------- | ------------ | ----------------------------- |
| `Lazy` (0)  | No            | No               | None         | Large datasets, optional data |
| `Eager` (1) | Yes           | Yes              | Module init  | Small core data, hot path     |

## Generated components

| Backend  | ModSupport | Files                                           |
| -------- | ---------- | ----------------------------------------------- |
| `None`   | `false`    | Core struct + enum + FrozenDictionary           |
| `Json`   | `false`    | Core + `ItemJson` + `ItemJsonRepository`        |
| `Sqlite` | `false`    | Core + `ItemSql` + `ItemSqlRepository`          |
| any      | `true`     | Core + JSON + `ItemMod` + adapter notifications |

## Examples

| Example                                         | Shows                                                                         |
| ----------------------------------------------- | ----------------------------------------------------------------------------- |
| [`FrifloPlugin/`](examples/FrifloPlugin/)       | Bridge data → Friflo Entity + components; register QuerySystem via IModPlugin |
| [`GodotPlugin/`](examples/GodotPlugin/)         | Bridge data → ItemNode (C# subclass); mod plugin spawning nodes               |
| [`ScriptingBridge/`](examples/ScriptingBridge/) | Lua VM exposing DataCatalyst + ECS API; generated typed methods               |

## Extension points

| Extension        | Interface                            | Registry                  |
| ---------------- | ------------------------------------ | ------------------------- |
| Engine adapter   | `IDataViewAdapter<T>`                | `DataViewAdapterRegistry` |
| DSL reader       | `IDslReader<T>`                      | `DslReaderRegistry`       |
| C# mod plugin    | `IModPlugin`                         | `PluginRegistry`          |
| Custom backend   | `IDataRepository<TKey, TValue>`      | `Item.Repository = ...`   |
| Generator plugin | `IPrimitiveTypeRule`, `ITypeEmitter` | `DcPluginRegistry`        |

## License

MIT
