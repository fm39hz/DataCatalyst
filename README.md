# DataCatalyst

**Data protocol layer for games.** Translates data from any source (JSON, CSV, YAML, DB, mod files) into strongly-typed C# structs at compile time. Zero reflection. NativeAOT-safe. Engine-agnostic.

Like MCP for AI agents — DataCatalyst standardizes how game data is defined, accessed, and overridden.

---

## Concept

```
Source (any format)         DataCatalyst              Game code
─────────────────    ─────────────────────    ─────────────────
JSON / CSV / YAML    → IDslReader → struct   → ItemsContext.FireSword.Damage
Database / API       → IDslReader → struct   → ItemsContext.Potion.Health
Mod overrides        → DataMerge → override  → direct field access
DSL (state-engine)   → DataDslRegistry       → type safe runtime
```

---

## Plugin system

```csharp
[DataPlugin(DependsOn = [typeof(CorePlugin)])]
public class StateEnginePlugin : IDataPlugin {
    static StateEnginePlugin() {
        DataDslRegistry.Register<BehaviorGroup>();
    }
}
```

- `IDataPlugin` — marker interface, 0 methods
- `[DataPlugin]` — class-level, `DependsOn` by Type ref
- Static constructor = plugin entry point
- Source gen: topo-sort → `PluginRegistry.Register<T>()` via `[ModuleInitializer]`

---

## Data Root — JSON → struct

```
[assembly: DataRoot("Data/Items")]

Data/Items/
├── _schema.json     { "kind": "Weapon", "fields": { "Damage": { "type": "int" } } }
├── Sword.json       { "inherits": "Weapon", "defaults": { "Damage": 25 }, "load": "compile" }
├── Potion.json      { "inherits": "Weapon", "defaults": { "Damage": 10 }, "load": "startup" }
└── states/behavior.json  { "$dsl": "BehaviorGroup", "Name": "Idle", ... }
```

Generated:
```csharp
namespace Data.Items {
    public struct Sword { public int Damage; }
    public struct Potion { public int Damage; }

    public static partial class ItemsContext {
        public static readonly Sword Sword = new() { Damage = 25 };  // compile-eager
        public static Potion Potion { get; private set; }

        [ModuleInitializer]
        internal static void Init() => DataContextRegistry.Register(Initialize);

        public static void Initialize(IReadOnlyList<DataOverride>? overrides) { ... }
    }
}
```

Access:
```csharp
int dmg = ItemsContext.Sword.Damage;   // direct field, zero cost
int hp  = ItemsContext.Potion.Damage;  // init-once
```

---

## Data context — runtime init + override

```csharp
DataContextRegistry.Register(Initialize);   // [ModuleInitializer] tự gọi
DataContextRegistry.InitializeAll(overrides); // apply base + mod
```

---

## Data merge — priority resolution

```csharp
DataMerge.Load(new[] {
    DataSource.From("Content/Data/", priority: 0),
    DataSource.From("Mods/Items/",   priority: 10),
});

DataMerge.OnConflict += (target, field, oldVal, newVal)
    => Console.WriteLine($"Conflict: {target}.{field}: {oldVal} → {newVal}");
```

---

## DSL registry — type-safe runtime

```csharp
DataDslRegistry.Register<BehaviorGroup>();
DataDslRegistry.IsRegistered<BehaviorGroup>();
var data = DataDslRegistry.Deserialize<BehaviorGroup>(json);
```

---

## Format extensibility — IDslReader

```csharp
public interface IDslReader<TValue> {
    string FileExtension { get; }
    bool TryRead(string text, out TValue value);
}

// Custom reader:
public sealed class YamlReader : IDslReader<Item> {
    public string FileExtension => ".yaml";
    public bool TryRead(string text, out Item value) { ... }
}

DslReaderRegistry.Register(new YamlReader());
```

---

## API surface

| Type | Purpose |
|---|---|
| `IDataPlugin` | Plugin marker |
| `[DataPlugin]` | Plugin declaration |
| `PluginRegistry` | `Register<T>()` |
| `[assembly: DataRoot("path")]` | Data folder declaration |
| `DataContextRegistry` | Init + override data |
| `DataDslRegistry` | DSL type registration + deserialize |
| `DataMerge` | Priority data resolution |
| `DataSource` | Source dir + priority |
| `DataOverride` | Single data patch |
| `IDataRepository<TKey,TValue>` | Data access contract |
| `IDataViewAdapter<T>` | Data change notification |
| `IDslReader<TValue>` | Format reader contract |
| `DslReaderRegistry` | Reader registration |
| `DataRef<TTarget,TTargetKind>` | Typed cross-reference |

## License

MIT
