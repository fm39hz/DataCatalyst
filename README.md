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
```

---

## Modding plugin - ladder of capabilities

DataCatalyst.Modding provides a **spectrum** of modding power. Game dev picks the ring that fits their game:

| Ring                  | Mechanism                                    | Drop-in    | NativeAOT | Game dev effort        | Modder power    |
| --------------------- | -------------------------------------------- | ---------- | --------- | ---------------------- | --------------- |
| **1 - Data**          | `ItemMod.LoadMods()` JSON override           | ✅         | ✅        | auto (detect plugin)   | Data only       |
| **2 - Script opt-in** | `[ModHook]` attribute + script `Hook.Before` | ✅         | ✅        | `[ModHook]` on methods | Script at hooks |
| **3 - Script auto**   | `EnableModHookWeaver = true`                 | ✅         | ✅        | MSBuild property       | Any method      |
| **4 - C# merge**      | `EnableModMerge = true`                      | ❌ rebuild | ✅        | MSBuild property       | Full C#         |

### Ring 1 - Data-only (CP-class)

```csharp
[CatalystData("Items.json")]
public partial struct Item { }

// Mods folder: items_override.json
ItemMod.LoadMods("Mods/Items/");
ItemMod.AddEntry("SuperPotion", new() { Health = 999 });

// Adapter - engine notified on every change
public class EcsItemAdapter : IDataViewAdapter<Item> {
    public void OnEntryAdded(string key, Item entry) { /* create entity */ }
    public void OnEntryRemoved(string key) { /* destroy entity */ }
    public void OnAllCleared() { /* destroy all */ }
}
```

Source gen produces `ItemExposer` - engine-agnostic typed bridge for scripting.

### Ring 2 - Script opt-in (`[ModHook]`)

Game dev marks methods mods can intercept:

```csharp
[ModHook]
public bool AddItem(Item i) { ... }

[ModHook("Player.TakeDamage")]
public void TakeDamage(int dmg) { ... }
```

Mod Lua script:

```lua
Hook.Before("Game.AddItem", function(call)
    if call.Args[1].Health > 100 then
        return true  -- skip, item too powerful
    end
end)

Hook.After("Player.TakeDamage", function(call)
    Log("took damage: " .. call.Args[1])
end)
```

`[ModHook]` weaver chạy ở build time - inject dispatch vào IL. Không runtime IL patching.

### Ring 3 - Script auto-weave (experimental)

```xml
<PropertyGroup>
  <EnableModHookWeaver>true</EnableModHookWeaver>
</PropertyGroup>
```

Weaver auto-inject dispatch vào mọi method public. Mod hook bất kỳ method nào.

### Ring 4 - C# merge (experimental)

```xml
<PropertyGroup><EnableModMerge>true</EnableModMerge></PropertyGroup>
```

```
Mods/
├── SkillPack/
│   ├── mod.json
│   ├── Skills.cs                ← compiled
│   └── skills_override.json     ← AdditionalFiles
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

---

## Architecture

```
DataCatalyst.Plugins.Modding/
├── SourceGen/
│   ├── EntryExposerEmitter.cs      ← sinh ItemExposer (Dictionary↔struct mapping, zero reflection)
│   └── ModdingGenerator.cs          ← emit [ModHook] attribute
│
├── Runtime/                         ← FM39hz.DataCatalyst.Plugins.Modding.Runtime
│   ├── ModLoader.cs                 ← Scan + manifest + lifecycle
│   ├── ModManifest.cs               ← mod metadata model
│   ├── EntryExposerRegistry.cs      ← bridge source gen → script context
│   ├── ComponentSchemaRegistry.cs   ← runtime component type def
│   ├── HookDispatcher.cs            ← IL entry point (Before/After)
│   └── HookRegistry.cs              ← methodId → script context registration
│
└── Weaver/                          ← FM39hz.DataCatalyst.Plugins.Modding.Weaver
    ├── WeaverTask.cs                 ← MSBuild AfterTargets=CoreCompile
    ├── HookInjector.cs               ← Mono.Cecil IL transformation
    └── build/*.props                 ← MSBuild integration
```

### Source gen: EntryExposer

Sinh per-catalog, engine-agnostic:

```csharp
// Generated - ItemExposer.cs
public static class ItemExposer {
    [ModuleInitializer]
    internal static void Register() =>
        EntryExposerRegistry.Register(Expose);

    private static void Expose(IScriptContext ctx) {
        ctx.SetGlobal("Item_Get",
            (Func<string, Dictionary<string, object>?>)(kind => MapToDict(Item.Get(kind))));
        ctx.SetGlobal("Item_Add",
            (Action<string, Dictionary<string, object>>)((key, fields) =>
                ItemMod.AddEntry(key, MapFromDict(fields))));
    }

    // Compile-time mapping - zero reflection
    private static Item MapFromDict(Dictionary<string, object> f) => new() {
        Health = (int)(f.TryGetValue("Health", out var h) ? h : 0),
        Weight = (float)(f.TryGetValue("Weight", out var w) ? w : 0f),
    };
    private static Dictionary<string, object> MapToDict(Item e) => new() {
        ["Health"] = e.Health, ["Weight"] = e.Weight,
    };
}
```

### IL weaver: HookDispatcher

```
Game C# → MSBuild → CoreCompile → WeaverTask → CoreCompile output (IL with hooks) → NativeAOT
```

For each method matching filter (`[ModHook]` or auto-weave):

```il
// IL before:
.method public bool AddItem(Item i) { ... ret ... }

// IL after:
.method public bool AddItem(Item i) {
    // injected - before original
    if (HookDispatcher.Before("Game.AddItem", this, args, out ret))
        return unbox(ret);

    // original body...

    // injected - before return
    HookDispatcher.After("Game.AddItem", this, stloc_return);
    ret;
}
```

### Mod manifest

```json
{
	"id": "FishingOverhaul",
	"version": "1.0.0",
	"gameVersion": ">=1.0.0",
	"dependencies": [{ "id": "CoreLib", "version": ">=1.2.0" }],
	"content": [
		{ "type": "component", "file": "components.json" },
		{ "type": "script", "file": "init.lua" },
		{ "type": "data", "file": "items_override.json", "target": "Item" }
	]
}
```

---

## Generated API

| Member                                           | Description                                              |
| ------------------------------------------------ | -------------------------------------------------------- |
| `Item.Potion`                                    | Static field per JSON entry (Eager mode)                 |
| `Item.Get(ItemKind)`                             | O(1) FrozenDictionary lookup (Eager) / Repository (Lazy) |
| `Item.Query(predicate)`                          | Filter entries                                           |
| `Item.Repository`                                | Swappable IDataRepository                                |
| `ItemMod.AddEntry(key, value)`                   | Mod data injection                                       |
| `ItemExposer.Expose(ctx)`                        | Script bridge registration                               |
| `DataRef<Buff, BuffKind>`                        | Typed cross-catalog reference                            |
| `CatalogRegistry.GetAll()`                       | All discovered catalogs                                  |
| `HookRegistry.RegisterBefore(methodId, ctx)`     | Script hook registration                                 |
| `HookDispatcher.Before(id, inst, args, out ret)` | IL dispatch entry point                                  |

## Generated components

| Backend  | ModSupport | Files                                    |
| -------- | ---------- | ---------------------------------------- |
| `None`   | `false`    | Core struct + enum + Repository          |
| `Json`   | `false`    | Core + `ItemJson` + `ItemJsonRepository` |
| `Sqlite` | `false`    | Core + `ItemSql` + `ItemSqlRepository`   |
| any      | `true`     | Core + JSON + `ItemMod` + `EntryExposer` |

## Extension points

| Extension          | Interface                            | Registry                  |
| ------------------ | ------------------------------------ | ------------------------- |
| Engine adapter     | `IDataViewAdapter<T>`                | `DataViewAdapterRegistry` |
| DSL reader         | `IDslReader<T>`                      | `DslReaderRegistry`       |
| C# mod plugin      | `IModPlugin`                         | `PluginRegistry`          |
| Custom backend     | `IDataRepository<TKey,TValue>`       | `Item.Repository = ...`   |
| Generator plugin   | `IPrimitiveTypeRule`, `ITypeEmitter` | `DcPluginRegistry`        |
| Script engine      | `IScriptEngine`                      | game-provided             |
| Script context     | `IScriptContext`                     | per-mod                   |
| Component registry | `IComponentSchemaRegistry`           | runtime type def          |
| Method hook        | `[ModHook]` attribute / auto-weave   | `HookRegistry`            |

## Backend switching

```csharp
[CatalystData("Items.json", Backend = DataBackendConst.All)]
public partial struct Item { }
```

**Env**: `DATACATALYST_BACKEND`, `DATACATALYST_CONNECTION`, `DATACATALYST_DATAPATH`.

## Cross-catalog references

```csharp
[CatalystData("buffs.json", typeof(BuffTemplate))]
public partial struct Buff { }

[CatalystData("items.json", typeof(ItemTemplate), RefTo = [typeof(Buff)])]
public partial struct Item { }

var buff = Buff.Get(item.BuffRef.Kind);
```

## Examples

| Example                                                               | Shows                           |
| --------------------------------------------------------------------- | ------------------------------- |
| [`modding_plugins/HelloMod/`](examples/modding_plugins/HelloMod/)     | Drop-in data override + script  |
| [`modding_plugins/WeaverHook/`](examples/modding_plugins/WeaverHook/) | `[ModHook]` + Hook.Before/After |
| [`FrifloPlugin/`](examples/FrifloPlugin/)                             | Bridge data → Friflo ECS        |
| [`GodotPlugin/`](examples/GodotPlugin/)                               | Bridge data → Godot             |
| [`ScriptingBridge/`](examples/ScriptingBridge/)                       | Lua scripting layer             |

## License

MIT
