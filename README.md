# DataCatalyst

Roslyn source generator: **JSON or any kind of Data stored into strongly-typed C# struct** at compile time. Zero reflection. NativeAOT-safe. Engine-agnostic.

## Why DataCatalyst?

In data-driven game development (especially using ECS architectures), managing static data often introduces boilerplate, performance overhead, or fragile magic strings. Existing configuration and serialization libraries typically rely on runtime reflection, heavy heap allocations, or force a strict coupling between code and raw asset files.

> This is not a drop-in replacement for any runtime data, repository or serializer, it is immutable by design
> Data driven, itself is not stat tuning, it is to parameterized game world. So this is not suitable for heavy CSV style tuning, as it is not designed for that things

DataCatalyst was built to enforce a strict separation of concerns between game design and development:

- **Zero Hardcoding:** Moves game values, schemas, and static data out of C# source code and into declarative JSON files.
- **Pure Clean Code:** Designers modify JSON definitions safely without touching the codebase, while developers interact with pure, type-safe C# structures.
- **Compile-Time Safety:** Catches data structure mismatches at compile time rather than crashing at runtime.

---

## Requirements

- .NET 8+ / .NET Standard 2.0+
- C# 12+

## Quick start

```bash
dotnet add package DataCatalyst

```

```xml
<ItemGroup>
  <AdditionalFiles Include="Data\*.json" />
</ItemGroup>

```

```csharp
[DataRoot]
public static partial class GameData { }

```

```json
// Data/Items/_schema.json
{ "kind": "Weapon", "fields": { "Damage": { "type": "int" } } }

// Data/Items/Sword.json
{ "inherits": "Weapon", "defaults": { "Damage": 25 }, "load": "compile" }

```

```csharp
int dmg = GameData.Items.Sword.Damage;   // 25, baked at compile time

```

---

## Core Concepts

### Source Generation Mapping

The `[DataRoot]` attribute triggers an incremental source generator to map file-system structures into compile-time types.

- **What:** Directories map to nested class branches; `.json` files map to strongly-typed C# structs.
- **Why:** Provides intuitive, predictable API access paths (e.g., `GameData.Items.Armor.Helmet`) that match your physical project structure without manual scaffolding.

### Schema vs. Data Files

- **Schema (`_*.json`):** Defines the structural blueprint (types and fields). Used to enforce data layout constraints across multiple instances.
- **Data (`*.json`):** Defines concrete data instances. Supports prototyped inheritance (`inherits`) and default value initialization.

### Load Modes

Controls the lifecycle and mutability of generated data fields.

| Mode                  | What it Generates                    | Concept                                                                                                         |
| --------------------- | ------------------------------------ | --------------------------------------------------------------------------------------------------------------- |
| `compile` _(Default)_ | `static readonly T` field            | Maximum performance. Values are baked directly at build time; zero runtime overhead.                            |
| `startup`             | `static T` property + `Initialize()` | Extensibility. Allows data to be modified or hot-reloaded via `DataContextRegistry` during game initialization. |

### Supported Field Types

Maps standard JSON definitions directly to optimized, allocation-free C# types:

- **Primitives:** `int`, `float`, `string`, `bool`.
- **Collections:** `ImmutableArray<T>` (via `array`) to ensure structural immutability.
- **Complex Types:** Nested structures (via `object`) and safe cross-references (via `DataRef<T>`).

---

## Format Extensibility

JSON is the default format. Any format can be added via `IDslReader<T>`:

```csharp
public interface IDslReader<TValue> {
    string FileExtension { get; }
    bool TryRead(string text, out TValue value);
}

public sealed class YamlReader : IDslReader<Item> {
    public string FileExtension => ".yaml";
    public bool TryRead(string text, out Item value) { /* parse YAML */ }
}

DslReaderRegistry.Register(new YamlReader());

```

At compile time, the source generator reads `<AdditionalFiles>` (JSON by convention). At runtime, `IDslReader` handles additional formats (YAML, CSV, custom DSL).

---

## Plugin System

A plugin is any class implementing `IDataPlugin` (marker interface, no methods) with a static constructor.

```csharp
public record struct FishingRod(string Name, int Power);

[DataPlugin(DependsOn = [typeof(CorePlugin)])]
public class FishingMod : IDataPlugin {
    static FishingMod() {
        // Register DSL types for runtime deserialization
        DataDslRegistry.Register<FishingRod>();
        // Load overrides from mod directory at startup
        var modOverrides = DataFileLoader.LoadFromDirectory("Mods/");
        DataContextRegistry.InitializeAll(modOverrides);
    }
}

```

The source generator detects `IDataPlugin` types, topo-sorts by `DependsOn`, and emits:

```csharp
[ModuleInitializer]
internal static void Init() {
    PluginRegistry.Register<CorePlugin>();
    PluginRegistry.Register<FishingMod>();
}

```

Plugins are optional. A project without plugins works the same — `[DataRoot]` still generates structs, `DataMerge` still merges data.

---

## Data Override (Modding Support)

```csharp
// Content/Data/items.json:  { "Sword": { "Damage": 99 } }

var overrides = new List<DataOverride>();
overrides.AddRange(DataFileLoader.LoadFromDirectory("Content/Data/"));
overrides.AddRange(DataFileLoader.LoadFromDirectory("Mods/"));
DataContextRegistry.InitializeAll(overrides);
```

Game code decides load order — later sources override earlier on conflict. Override JSON uses struct name as key, raw JSON values (no boxing, typed parsing).

---

## DSL Registry (Runtime Deserialization)

```csharp
// Register (typically in a plugin's static constructor):
DataDslRegistry.Register<FishingRod>();

// Later, at runtime:
var json = """{ "Name": "Ultra Rod", "Power": 99 }""";
var rod = DataDslRegistry.Deserialize<FishingRod>(json);
Console.WriteLine(rod.Name); // "Ultra Rod"

```

Uses `System.Text.Json` under the hood. Custom options can be passed: `Register<T>(new JsonSerializerOptions { ... })`.

---

## Engine Adapter

```csharp
public class EcsItemAdapter : IDataViewAdapter<Item> {
    public void OnEntryAdded(string key, Item entry) { /* create entity */ }
    public void OnEntryRemoved(string key) { /* destroy entity */ }
    public void OnAllCleared() { /* destroy all */ }
}
DataViewAdapterRegistry.Register<Item>(new EcsItemAdapter(store));

```

---

## Performance & Compatibility

- **Runtimes:** Fully compatible with `.NET Standard 2.0+` and `.NET 8+`. Target-agnostic (Mono, IL2CPP, NativeAOT, CoreCLR).
- **Game Engines:** Agnostic implementation. Out-of-the-box support for Godot, Stride, Unity (via modern MSBuild pipelines), or custom ECS frameworks (Arch, Friflo, DefaultEcs, etc...).
- **Memory & GC:** Emits lightweight `struct` models. Zero runtime heap allocations during data retrieval.
- **AOT-Ready:** Eliminates runtime Reflection completely. 100% linker/trimmer safe.
- **Build Performance:** Implemented as an `IncrementalGenerator` to ensure minimal CPU/memory overhead during IDE editing and rapid iterative compilation.

---

## Generated API

| Member                                | Description                                   |
| ------------------------------------- | --------------------------------------------- |
| `GameData.Items.Sword`                | Static readonly field (compile mode)          |
| `GameData.Items.Potion`               | Static property, populated at startup         |
| `DataContextRegistry.InitializeAll()` | Apply overrides to startup-mode data          |
| `DataContextRegistry.Register<T>()`   | Typed override handler registration           |
| `DataFileLoader.LoadFromDirectory()`  | Read override JSON from a directory           |
| `DataDslRegistry.Deserialize<T>()`    | JSON → typed object (plugin-registered types) |
| `DataRef<T>`                          | Typed cross-reference by string key           |
| `PluginRegistry.Register<T>()`        | Plugin registration (via source gen)          |

## Extension Points

| Extension           | Interface                                   | Mechanism                         |
| ------------------- | ------------------------------------------- | --------------------------------- |
| Data format         | `IDslReader<T>`                             | `DslReaderRegistry`               |
| Engine bridge       | `IDataViewAdapter<T>`                       | `DataViewAdapterRegistry`         |
| Plugin              | `IDataPlugin`                               | `[DataPlugin]` + `PluginRegistry` |
| Data repository     | `IDataRepository<TKey,TValue>`              | User-provided                     |
| Source gen pipeline | `IScanner`, `IGraphBuilder`, `ICodeEmitter` | Internal, replaceable             |

## License

MIT
