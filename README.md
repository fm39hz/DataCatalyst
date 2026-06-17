# DataCatalyst

Foundational framework for data-driven composition. Enforces strict separation between code (mechanism) and content (data).

- **Dev** declares primitives via `[DataPrimitive]` C# attributes — components, traits, signals, readers.
- **Designer** composes primitives via external data files (JSON, YAML, etc.) — zero code, zero DSL.
- **DataCatalyst** resolves composition mechanically — inheritance, references, validation → immutable `DataCatalog`.
- **Consumer** reads catalog, instantiates game objects mechanically. No game-specific branching in systems.

## Philosophy

> Code, tự thân nó không có content. Data, script, behavior,… đều không nên nằm trong code. Nó nằm ở nơi chứa data khởi tạo, immutable.

DataCatalyst is a **framework**, not a tool:
- NOT a DSL
- NOT an authoring tool
- NOT a behavior engine
- NOT a source gen that bakes content into structs

## Requirements

- .NET 8+ / .NET Standard 2.0+
- C# 12+

## Quick Start

```bash
dotnet add package DataCatalyst
```

```csharp
// 1. Dev declares primitives
[DataPrimitive]
public struct Health : IComponent { public float Current, Max; }

[DataPrimitive]
public struct Position : IComponent { public float X, Y, Z; }
```

```json
// 2. Designer composes via data
// Data/Entities/Hero.json
{
  "inherits": ["Data/Entities/BaseHumanoid"],
  "components": {
    "Health": { "Current": 100, "Max": 100 },
    "Position": { "X": 0, "Y": 0, "Z": 0 }
  }
}
```

```csharp
// 3. DataCatalyst resolves → immutable catalog
var rawEntries = DataFileLoader.LoadFromDirectory("Data/Entities");
var graph = DataGraphBuilder.Build(rawEntries);
DataResolution.DeserializeComponents(graph, name => PrimitiveRegistry.GetAll()
    .FirstOrDefault(t => t.Name == name));
var catalog = DataCatalogBuilder.Resolve(graph);

// 4. Consumer reads catalog mechanically
foreach (var entry in catalog.Entries) {
    var entity = store.CreateEntity();
    if (entry.TryGet<Health>(out var hp)) entity.AddComponent(hp);
    if (entry.TryGet<Position>(out var pos)) entity.AddComponent(pos);
}
```

## Architecture

```
┌─────────────────────────────────────────────────────┐
│  Consumer Layer (game/engine)                        │
│  - Declares primitives via [DataPrimitive]           │
│  - Reads resolved DataCatalog                        │
│  - Interprets composition mechanically               │
├─────────────────────────────────────────────────────┤
│  DataCatalyst.Core  (netstandard2.0)                 │
│  - [DataPrimitive] attribute                         │
│  - PrimitiveRegistry, DataGraph, DataCatalogBuilder  │
│  - DataFileLoader, DataResolution, DataValidation    │
├─────────────────────────────────────────────────────┤
│  DataCatalyst.Abstractions  (netstandard2.0, zero dep)│
│  - DataKey<T>, IDataPlugin, IDataViewAdapter<T>      │
│  - IDslReader<T>, IDataRepository                    │
├─────────────────────────────────────────────────────┤
│  DataCatalyst.SourceGen  (netstandard2.0, required)  │
│  - PrimitiveDiscoveryGenerator                       │
│  - Scans for [DataPrimitive] types                   │
│  - Generates PrimitiveRegistrations.g.cs             │
└─────────────────────────────────────────────────────┘
```

## Core Concepts

### DataPrimitive

Any C# type marked with `[DataPrimitive]` — struct, class, enum, or method.
DataCatalyst only cares about the TYPE, not what it means.
Primitives are discovered at compile time by the source generator.

### DataGraph → DataCatalog

1. **Load**: `DataFileLoader.LoadFromDirectory()` reads JSON files → `RawDataEntry` list
2. **Build**: `DataGraphBuilder.Build()` → `DataGraph` (raw graph with inheritance chains)
3. **Resolve**: `DataCatalogBuilder.Resolve()` → `DataCatalog` (flattened, validated, immutable)
4. **Deserialize**: `DataResolution.DeserializeComponents()` → typed components via `PrimitiveRegistry`

### DataEntry

A single composition entry with:
- `Key` (string identifier from file path)
- `Inherits` (optional parent keys)
- `Get<T>()` / `TryGet<T>()` / `Has<T>()` for typed component access

### DataCatalog

Resolved, immutable output:
- `Entries` — `IReadOnlyDictionary<string, DataEntry>`
- `Get<T>(key)` / `TryGet<T>(key, out T)` for typed lookup

## Existing Files to Keep

| File | Role |
|------|------|
| `IDataViewAdapter<T>` | Engine bridge (add/remove/modify/clear callbacks) |
| `IDslReader<T>` | Custom format reader (YAML, etc.) |
| `IDataPlugin` | Plugin marker |
| `IDataRepository<TKey, TValue>` | Generic repository interface |
| `DataKey<T>` | Typed cross-reference by string key |
| `PluginRegistry` | Plugin instance registry |
| `DataViewAdapterRegistry` | View adapter registry |
| `DslReaderRegistry` | DSL reader registry |
| `ServiceRegistry` | Service locator |

## Extension Points

| Extension | Interface | Mechanism |
|-----------|-----------|-----------|
| Data format | `IDslReader<T>` | `DslReaderRegistry` |
| Engine bridge | `IDataViewAdapter<T>` | `DataViewAdapterRegistry` |
| Plugin | `IDataPlugin` | `[DataPlugin]` + `PluginRegistry` |
| Data repository | `IDataRepository<TKey,TValue>` | User-provided |

## License

MIT
