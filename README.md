# DataCatalyst

Roslyn source generator: **JSON → strongly-typed C# struct + enum + FrozenDictionary** at compile time. Zero reflection. NativeAOT-safe. Engine-agnostic.

```
dotnet add package FM39hz.DataCatalyst
```

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
```

---

## Modding plugin

`FM39hz.DataCatalyst.Plugins.Modding` adds drop-in mod support to all `[CatalystData]` types. Mods can override data and run script logic — no rebuild required.

Reference the plugin → source gen auto-detects and generates `ItemMod`, `EntryExposer`, mod-aware `Get()` for every catalog. No per-type opt-in.

### Data override

```csharp
[CatalystData("Items.json")]
public partial struct Item { }

ItemMod.LoadMods("Mods/Items/");
ItemMod.AddEntry("SuperPotion", new() { Health = 999 });
```

Engine notified on every change via `IDataViewAdapter<Item>`:

```csharp
public class EcsItemAdapter : IDataViewAdapter<Item> {
    public void OnEntryAdded(string key, Item entry) { /* create entity */ }
    public void OnEntryRemoved(string key) { /* destroy entity */ }
    public void OnAllCleared() { /* destroy all */ }
}
```

### Scripted mods

```
Mods/MyMod/
├── mod.json
├── init.lua             ← script logic
└── items_override.json  ← data override
```

Game startup:
```csharp
ModLoader.LoadAllSources(new[] {
    ModSource.From("Data/"),
    ModSource.From("Mods/", 10),
}, new LuaScriptEngine(), new ComponentSchemaRegistry());
```

2-phase lifecycle: content (all mods, sorted) → init (per-mod, sorted). Script accesses data via typed bridge:

```lua
Item_Add("SuperSword", { Health = 50, Weight = 3 })
local item = Item_Get("Potion")
```

### C# merge (player in build phase, but not drop-in at runtime)

```xml
<PropertyGroup><EnableModMerge>true</EnableModMerge></PropertyGroup>
```

```csharp
[ModPlugin("SkillPack", ["CoreLib@1.2.1"])]
public class SkillPack : IModPlugin {
    public void OnLoad(IModGameContext ctx) {
        ItemMod.AddEntry("SuperPotion", new() { Health = 500 });
        ctx.GetService<ISystemRegistry>()?.Register(new CombatLevelSystem());
    }
}
```

### Scope

DataCatalyst handles **data** (JSON → typed struct) and **mod hosting** (scan + lifecycle + script bridge). It does NOT replace mod loaders:

| Feature | Belongs to |
|---|---|
| IL method patching | Mod loader (SMAPI/SKSE/Fabric) |
| Cross-mod API | Mod loader |
| Event bus | Game dev |
| Asset pipeline | Engine |
| Package manager | Platform store |

---

## Architecture

```
DataCatalyst.Plugins.Modding/
├── SourceGen/
│   ├── EntryExposerEmitter.cs     ← typed script bridge (Dictionary↔struct)
│   └── ModOverlayDataEmitter.cs   ← ItemMod (AddEntry/RemoveEntry/LoadMods)
│
├── Runtime/
│   ├── ModLoader.cs               ← Scan + 2-phase lifecycle
│   ├── ModManifest.cs             ← mod metadata
│   ├── ModLoadResult.cs           ← per-mod result
│   ├── ModSource.cs               ← unified content source
│   ├── EntryExposerRegistry.cs    ← source gen → script context bridge
│   └── ComponentSchemaRegistry.cs ← runtime component type registration
│
└── Abstractions (in core):
    ├── IScriptEngine              ← scripting engine factory
    ├── IScriptContext              ← per-mod sandbox
    └── IComponentSchemaRegistry   ← runtime type registry
```

Source gen emits per-catalog:

```csharp
// ItemExposer.cs — script bridge, zero reflection
public static class ItemExposer {
    [ModuleInitializer]
    internal static void Register() =>
        EntryExposerRegistry.Register(Expose);

    private static void Expose(IScriptContext ctx) {
        ctx.SetGlobal("Item_Get", (Func<string, Dictionary<string, object>?>)(kind =>
            MapToDict(Item.Get(Item.GetKind(kind)))));
        ctx.SetGlobal("Item_Add", (Action<string, Dictionary<string, object>>)((key, f) =>
            ItemMod.AddEntry(key, MapFromDict(f))));
    }

    private static Item MapFromDict(Dictionary<string, object> f) => new() {
        Health = (int)(f.TryGetValue("Health", out var h) ? h : 0),
    };
    private static Dictionary<string, object> MapToDict(Item e) => new() {
        ["Health"] = e.Health,
    };
}
```

---

## Generated API

| Member | Description |
|---|---|
| `Item.Potion` | Static field per JSON entry (Eager) |
| `Item.Get(ItemKind)` | O(1) lookup — mod-aware if plugin present |
| `Item.Query(predicate)` | Filter entries |
| `Item.Repository` | Swappable IDataRepository |
| `ItemMod.AddEntry(key, value)` | Mod data injection |
| `ItemExposer.Expose(ctx)` | Script bridge registration |
| `DataRef<Buff, BuffKind>` | Typed cross-catalog reference |
| `CatalogRegistry.GetAll()` | All discovered catalogs |

## Generated components

| Backend | Plugin present | Files |
|---|---|---|
| `None` | No | Core struct + enum + Repository |
| `Json` | No | Core + `ItemJson` + `ItemJsonRepository` |
| `Sqlite` | No | Core + `ItemSql` + `ItemSqlRepository` |
| any | Yes | Core + JSON + `ItemMod` + `EntryExposer` |

## Extension points

| Extension | Interface | Registry |
|---|---|---|
| Engine adapter | `IDataViewAdapter<T>` | `DataViewAdapterRegistry` |
| DSL reader | `IDslReader<T>` | `DslReaderRegistry` |
| C# mod plugin | `IModPlugin` | `PluginRegistry` |
| Custom backend | `IDataRepository<TKey,TValue>` | `Item.Repository = ...` |
| Generator plugin | `IPrimitiveTypeRule`, `ITypeEmitter` | `DcPluginRegistry` |
| Script engine | `IScriptEngine` | game-provided |
| Component schema | `IComponentSchemaRegistry` | `ModLoader` |

## Backend switching

```csharp
[CatalystData("Items.json", Backend = DataBackendConst.All)]
public partial struct Item { }
```

Env: `DATACATALYST_BACKEND`, `DATACATALYST_CONNECTION`, `DATACATALYST_DATAPATH`.

## Cross-catalog references

```csharp
[CatalystData("buffs.json", typeof(BuffTemplate))]
public partial struct Buff { }

[CatalystData("items.json", typeof(ItemTemplate), RefTo = [typeof(Buff)])]
public partial struct Item { }

var buff = Buff.Get(item.BuffRef.Kind);
```

## Examples

| Example | Shows |
|---|---|
| [`modding_plugins/HelloMod/`](examples/modding_plugins/HelloMod/) | Data override + manifest + script |
| [`FrifloPlugin/`](examples/FrifloPlugin/) | Bridge data → Friflo ECS |
| [`GodotPlugin/`](examples/GodotPlugin/) | Bridge data → Godot |

## License

MIT
