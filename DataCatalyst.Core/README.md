# DataCatalyst.Core

Composition resolution engine + registries. netstandard2.1.

## Pipeline

```
DataEntry (typed components + optional inherits)
  → DataGraphBuilder.Build() → DataGraph (unresolved)
  → DataCatalogBuilder.Resolve() → DataCatalog (resolved, immutable)
     - Inheritance flatten (DAG, deep chain)
     - Child override parent
     - Cycle detection
```

## Public API

| Type | Description |
|------|-------------|
| `PrimitiveRegistry` | `Register<T>()`, `IsRegistered(Type)`, `GetAll()`, `Clear()` |
| `PluginRegistry` | `Register<T>()`, `RegisterByTypeName()`, `Plugins` |
| `DataEntry` | Key + Inherits + `Set<T>()`/`Get<T>()`/`TryGet<T>()`/`Has<T>()` |
| `DataGraph` | `Dictionary<string, DataEntry>` entries |
| `DataGraphBuilder` | `Build(IEnumerable<DataEntry>) → DataGraph` |
| `DataCatalog` | `IReadOnlyDictionary<string, DataEntry>` + `Get<T>(key)`/`TryGet<T>(key)` |
| `DataCatalogBuilder` | `Resolve(DataGraph) → DataCatalog` |
| `DataOverride` | Raw JSON override (legacy compat) |
| `DataViewAdapterRegistry` | View adapter registration |
| `ServiceRegistry` | Generic service locator |

## NOT in Core

Serializer, format reader, file loader, entity model, behavior engine, DSL.
