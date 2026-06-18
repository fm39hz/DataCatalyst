# DataCatalyst.Core

The core composition and resolution engine for the DataCatalyst framework.

## 🔄 Composition Pipeline

DataCatalyst resolves compositions mechanically through a multi-stage pipeline:

```
JSON files
  ↳ JsonDataLoader.LoadDirectory() ──> LoadResult (entries + diagnostics)
      ↳ DataGraphBuilder.Build() ──> DataGraph (unresolved map, merges mod patches)
          ↳ DataCatalogBuilder.Resolve() ──> DataCatalog (resolved, flattened, immutable)
              ↳ DataMaterializer.Materialize() ──> consumer entities (via registered delegates)
```

During resolution, the engine processes:

1. **Component Resolution**: JSON property names are matched to component types via their short type name (the type-system-derived discriminator). No runtime `Type.Name` dictionary building — the mapping is compiled at build time by the source generator using `nameof()` constants.
2. **Mod Patches**: Automatically merges duplicate keys based on load order (the ContentPatcher pattern).
3. **Inheritance DAG**: Processes inheritance trees to flatten parents into children.
4. **Value Overrides**: Allows children to override parent components.
5. **Cycle Detection**: Validates that the dependency tree contains no cyclic loops (throws `InvalidOperationException`
   if a cycle is found).

## 📦 Public API

| Type                      | Description                                                                   |
|---------------------------|-------------------------------------------------------------------------------|
| `PrimitiveRegistry`       | Registry for component types and their JSON discriminators (`TryResolveId`). |
| `PluginRegistry`          | Centralized registry for initializing discovered custom plugins.              |
| `DataRegistry`            | Instance-based registry for manual component/plugin registration.             |
| `DataEntry`               | Immutable container for an entry's key, components, and inherits lists.       |
| `DataGraph`               | Unresolved key-value entry map.                                               |
| `DataGraphBuilder`        | Builds a `DataGraph` from a raw list of entries, merging duplicate keys/mods. |
| `DataCatalog`             | The resolved, immutable, read-only dictionary of composition entries.         |
| `DataCatalogBuilder`      | Resolves a `DataGraph` into a finalized `DataCatalog`.                        |
| `DataCatalogExtensions`   | Extension methods (`Bind<TKey,TValue>`) for batch lookups.                    |
| `DataMaterializer<T>`     | Registry and dispatcher for materializing components to game entities.        |
| `DataViewAdapterRegistry` | Registers observers/view adapters for engine-specific bridges.                |
| `ServiceRegistry`         | Lightweight, generic service locator.                                         |
| `DataOverride`            | Record model for runtime overlay applications (planned integration).          |
