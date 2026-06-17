# DataCatalyst.SourceGen

Roslyn incremental generator. Required at compile time.

## PrimitiveDiscoveryGenerator

Scans for `[DataComponent]` types and `IDataPlugin` + `[DataPlugin]` classes.
Emits `PrimitiveRegistrations.g.cs` with `[ModuleInitializer]`.

Scans both current compilation and referenced assemblies. Topo-sorts plugins by `DependsOn`.

```csharp
// Input
[DataComponent] public struct Health { public float Current, Max; }
[DataPlugin] public class MyPlugin : IDataPlugin { }

// Generated
[ModuleInitializer]
internal static void RegisterPrimitives() {
    PrimitiveRegistry.Register<Health>();
}
[ModuleInitializer]
internal static void Init() {
    PluginRegistry.Register<MyPlugin>();
}
```
