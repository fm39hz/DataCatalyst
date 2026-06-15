# DataCatalyst — Example Plugins

## Data flow

```mermaid
flowchart TB
    subgraph Input ["Mod Sources"]
        A1["JSON data file<br/><code>mods/items/weapons.json</code>"]
        A2["C# ModPlugin<br/><code>[ModPlugin] class</code>"]
        A3["Script mod<br/><code>mods/scripts/buff.lua</code>"]
    end

    subgraph DC ["DataCatalyst Core"]
        B1["<code>ItemMod.LoadMods()</code><br/>Parse JSON → RowData"]
        B2["<code>ItemMod.AddEntry()</code><br/>C# / Script injection"]
        B3["ItemMod._modEntries<br/>Dictionary<string, Item>"]
    end

    subgraph Events ["Reactive Notification"]
        C1["<code>DataViewAdapterRegistry<br/>.GetAdapters<Item>()</code>"]
    end

    subgraph Adapters ["Engine Adapters (one per engine)"]
        D1["<b>FrifloItemAdapter</b><br/>→ Entity + ItemRef component"]
        D2["<b>GodotItemAdapter</b><br/>→ ItemNode (C# subclass)"]
        D3["<b>UnityItemAdapter</b><br/>→ MonoBehaviour / ScriptableObject"]
    end

    subgraph Engine ["Engine Layer"]
        E1["Friflo ECS<br/>EntityStore + QuerySystem"]
        E2["Godot Scene<br/>ItemNode inherits Node"]
        E3["Unity Runtime<br/>MonoBehaviour updates"]
    end

    A1 -->|runtime| B1
    A2 -->|build-merge| B2
    A3 -->|runtime script VM| B2
    B1 --> B3
    B2 --> B3
    B3 -->|AddEntry / RemoveEntry / Clear| C1
    C1 -->|OnEntryAdded / OnEntryRemoved / OnEntryModified / OnAllCleared| D1
    C1 --> D2
    C1 --> D3
    D1 -->|CreateEntity + IComponent| E1
    D2 -->|AddChild + typed C# class| E2
    D3 -->|Instantiate + assign| E3

    style Input fill:#1a1a2e,stroke:#e94560,color:#fff
    style DC fill:#16213e,stroke:#0f3460,color:#fff
    style Events fill:#0f3460,stroke:#e94560,color:#fff
    style Adapters fill:#533483,stroke:#e94560,color:#fff
    style Engine fill:#1a1a2e,stroke:#e94560,color:#fff
```

## Dependency resolution

### Runtime data flow

```
Mod JSON file
    → ItemMod.LoadMods("mods/items/")
        → ItemJson.Read(ref Utf8JsonReader)     ← generated, no reflection
            → ItemMod.AddEntry(key, entry)
                → _modEntries[key] = entry
                    → DataViewAdapterRegistry.GetAdapters<Item>()
                        → GodotItemAdapter.OnEntryAdded()
                            → scene.AddChild(new ItemNode { Kind = kind })
```

DataCatalyst holds the single source of truth (`FrozenDictionary<Kind, T>` + `_modEntries`).  
Adapters store only the enum key — engine reads data through `Item.Get(Kind)`.

### Build-time code mods

```
Mod source (.cs + mod.json)
    → MSBuild EnableModMerge target
        → Mods/*/Source/*.cs → <Compile>
        → Mods/*/Data/*.json → <AdditionalFiles>
            → [ModuleInitializer] registers plugin
                → PluginRegistry.LoadAll(ctx)
                    → mod calls ItemMod.AddEntry / ctx.GetService<T>()
```

### Scripting layer

```
Mod Lua script
    → Game's ScriptBridge.LoadModScripts("mods/scripts/")
        → Script calls DataBridge.Add("Item", key, entry)
            → ItemMod.AddEntry(key, entry)         ← via DataCatalyst API
        → Script calls EcsBridge.RegisterSystem()
            → Friflo SystemRoot.Add(system)        ← via game's bridge
```

### Adapter responsibility

| Adapter | Creates | Stores | Reads data from |
|---|---|---|---|
| FrifloItemAdapter | Friflo Entity + `ItemRef` | `ItemKind` enum | `Item.Get(Kind)` |
| GodotItemAdapter | `ItemNode : Node` | `ItemKind` field | `Item.Get(Kind)` |
| UnityItemAdapter | `ItemMono : MonoBehaviour` | `ItemKind` field | `Item.Get(Kind)` |

No memory duplication — adapters store only the key, actual data lives once in `FrozenDictionary`.

## Scripting Bridge

Runtime code mods through a Lua VM. No rebuild, zero GC per tick, no string catalog names.

```
Game startup
    → new ScriptBridge(store, root)
        → registers typed C# methods as Lua globals:
            Data_AddItem(key, health, weight)   → ItemMod.AddEntry
            Data_AddBuff(key, power)             → BuffMod.AddEntry
            ECS.CreateEntity()                   → store.CreateEntity()
            ECS.RegisterSystem(name, def)        → adds ScriptSystem to pipeline
    → bridge.LoadModScripts("Mods/Scripts/")
        → loads skills.lua, auras.lua
            → Lua uses typed methods (no string catalogs)
            → ScriptSystem.OnUpdate:
                foreach entity → fn.Call(entityId, dt)
                  → int + float, zero allocation
```

| Layer | Responsibility | Example file |
|---|---|---|
| Bridge | Wires typed DataCatalyst + ECS to Lua VM | [`ScriptBridge.cs`](ScriptingBridge/ScriptBridge.cs) |
| Mod script | Adds data + registers logic | [`skills.lua`](ScriptingBridge/mods/skills.lua) |

**Key design choices:**
- **No `new Table()` per entity** — `ScriptSystem.OnUpdate` passes `(entityId, dt)` as int + float, zero allocation per frame
- **No string catalog lookup** — `Data_AddItem`, `Data_AddBuff` are typed C# delegates registered as Lua globals; script calls them directly
- **Entity ID, not object** — Lua receives entity IDs, reads/writes components through typed C# helpers
