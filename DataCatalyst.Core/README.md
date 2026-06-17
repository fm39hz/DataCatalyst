# DataCatalyst.Core

The core composition and resolution engine for the DataCatalyst framework.

## 🔄 Composition Pipeline

DataCatalyst resolves compositions mechanically through a multi-stage pipeline:

```
DataEntry (unresolved components + inherits)
  ↳ DataGraphBuilder.Build() ──> DataGraph (unresolved map, merges mod patches)
      ↳ DataCatalogBuilder.Resolve() ──> DataCatalog (resolved, flattened, immutable)
```

During resolution, the engine processes:

1. **Mod Patches**: Automatically merges duplicate keys based on load order (the ContentPatcher pattern).
2. **Inheritance DAG**: Processes inheritance trees to flatten parents into children.
3. **Value Overrides**: Allows children to override parent components.
4. **Cycle Detection**: Validates that the dependency tree contains no cyclic loops (throws `InvalidOperationException`
   if a cycle is found).

## 📦 Public API

| Type                      | Description                                                                   |
|---------------------------|-------------------------------------------------------------------------------|
| `PrimitiveRegistry`       | Centralized registry for known data component types.                          |
| `PluginRegistry`          | Centralized registry for initializing discovered custom plugins.              |
| `DataEntry`               | Container representing an entry's key, components, and inherits lists.        |
| `DataGraph`               | Unresolved key-value entry map.                                               |
| `DataGraphBuilder`        | Builds a `DataGraph` from a raw list of entries, merging duplicate keys/mods. |
| `DataCatalog`             | The resolved, immutable, read-only dictionary of composition entries.         |
| `DataCatalogBuilder`      | Resolves a `DataGraph` into a finalized `DataCatalog`.                        |
| `DataViewAdapterRegistry` | Registers observers/view adapters for engine-specific bridges.                |
| `ServiceRegistry`         | Lightweight, generic service locator.                                         |
