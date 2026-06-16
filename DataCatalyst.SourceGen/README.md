# DataCatalyst.SourceGen

Source generators for DataCatalyst.

## UniversalDataGenerator

Emits `[DataPlugin]` attribute. Detects `IDataPlugin` implementations, topo-sorts by `DependsOn`, emits `PluginRegistrations.g.cs`.

```csharp
[DataPlugin(DependsOn = [typeof(Other)])]
public class MyPlugin : IDataPlugin { }
```

Generated:
```csharp
[ModuleInitializer]
internal static void Init() {
    PluginRegistry.Register<MyPlugin>();
    PluginRegistry.Register<Other>();
}
```

## DataRootGenerator

Emits `[DataRoot]` attribute. Scans `Data/` folder (via AdditionalTextsProvider), parses JSON files, resolves inheritance, emits structs + context class.

Pipeline:
```
AdditionalTextsProvider (JSON files)
  → IScanner (DataRootScanner) — parse schema/data/DSL files
    → IGraphBuilder (InheritanceGraph) — resolve inherits, flatten fields
      → ICodeEmitter (NativePocoEmitter) — emit C# struct + branch context
```

Abstractions `IScanner`, `IGraphBuilder`, `ICodeEmitter` in `DataCatalyst.DataRoot` namespace.
