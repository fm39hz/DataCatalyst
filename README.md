# DataCatalyst

Foundational framework for data-driven composition. Code = mechanism, not content.

- **Dev** declares `[DataComponent]` types + `[DataPlugin]` plugins in C#
- **Designer** composes via external data (JSON/...) — zero code
- **DataCatalyst** resolves composition mechanically — `DataCatalogBuilder.Resolve()` → immutable `DataCatalog`
- **Consumer** reads catalog mechanically

## Philosophy

> Code tự thân không có content. Data, script, behavior đều không nên nằm trong code.

NOT a serializer, NOT a behavior engine, NOT a DSL.

## Projects

```
DataCatalyst.Abstractions    — contracts: [DataComponent], [DataPlugin], DataKey<T>, IDataPlugin
DataCatalyst.Core            — PrimitiveRegistry, PluginRegistry, DataEntry/Graph/Catalog + builders
DataCatalyst.SourceGen       — PrimitiveDiscoveryGenerator (required, netstandard2.0)
DataCatalyst.Loaders         — JsonDataLoader
DataCatalyst.Plugins.*       — composable infra plugins
  ├── NumericCompare         — CompareOp enum + OperatorParser
  ├── Transition             — TransitionDef, ConditionGroupDef, SensorConditionDef
  └── StateMachine           — StateGroup + StateMachineEvaluator (uses NumericCompare + Transition)
```

## Quick Start

```csharp
// 1. Dev declare component
using DataCatalyst.Abstractions;
[DataComponent] public struct Health { public float Current, Max; }

// 2. Load data
using DataCatalyst.Loaders;
using DataCatalyst.Core;
var catalog = DataCatalogBuilder.Resolve(
    DataGraphBuilder.Build(JsonDataLoader.LoadDirectory("Data/")));

// 3. Consumer read mechanically
foreach (var e in catalog.Entries.Values)
    if (e.TryGet<Health>(out var h)) spawn.Add(h);
```

## Core API

| Type | Role |
|------|------|
| `[DataComponent]` | Mark any type as composable data component |
| `[DataPlugin]` | Mark plugin class with optional `DependsOn` |
| `IDataPlugin` | Plugin marker interface |
| `PrimitiveRegistry` | `Register<T>()`, `IsRegistered()`, `GetAll()` |
| `PluginRegistry` | `Register<T>()`, `Plugins` |
| `DataEntry` | Key + Inherits + `Set<T>()`/`Get<T>()`/`TryGet<T>()` |
| `DataGraphBuilder` | `Build(entries) → DataGraph` |
| `DataCatalogBuilder` | `Resolve(graph) → DataCatalog` — inherits + topo sort + cycle detect |
| `DataKey<T>` | Typed cross-reference by string key |
| `IFormatReader<T>` | Optional format parser contract |

## Plugin Contract

```csharp
[DataPlugin(DependsOn = [typeof(OtherPlugin)])]
public class MyPlugin : IDataPlugin { }

[DataComponent]
public struct MyComponent { public int Value; }
```

Source gen discovers all `IDataPlugin` + `[DataComponent]` across current + referenced assemblies, topo-sorts by `DependsOn`, emits `ModuleInitializer`.

## Boundary

Core không biết: serializer, file format, entity model, game logic, behavior.
Core chỉ biết: `Type` + `object` — resolve composition.

## License

MIT
