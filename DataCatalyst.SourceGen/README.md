# DataCatalyst.SourceGen

Source generators for DataCatalyst.

## PrimitiveDiscoveryGenerator

Emits `[DataPlugin]` attribute. Scans for `[DataPrimitive]` and `IDataPlugin` types.
Generates `PrimitiveRegistrations.g.cs` with `ModuleInitializer` for both.

```csharp
[DataPrimitive]
public struct Health : IComponent { public float Current, Max; }

[DataPlugin(DependsOn = [typeof(Other)])]
public class MyPlugin : IDataPlugin { }
```

Generated:
```csharp
[ModuleInitializer]
internal static void Init() {
    PluginRegistry.Register<MyPlugin>();
    PrimitiveRegistry.Register<Health>();
}
```
