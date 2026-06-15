# DataCatalyst.SourceGen

Universal Data-Driven Source Generator for C# games. Reads JSON from `<AdditionalFiles>`, emits strongly-typed, **reflection-free** static catalogs. Native AOT / trim-safe.

---

## Architecture

```
UniversalDataGenerator (IIncrementalGenerator)
├── RegisterPostInitializationOutput
│   ├── CatalystDataAttribute.g.cs       ← [CatalystData], [ModPlugin]
│   └── DataCatalystRuntime.g.cs          ← runtime contracts
│
├── ForAttributeWithMetadataName[CatalystData]
│   └── PipelineDriver
│       ├── IEntryPointReader    → first match
│       ├── ISchemaProvider      → first match
│       ├── IPrimitiveTypeRule   → all (ranked)
│       ├── ITypeEmitter (main)  → first match — PartialStruct
│       ├── ITypeEmitter (companion) → ALL matching
│       │   ├── SqliteDataEmitter       ← Backend.HasFlag(Sqlite)
│       │   ├── JsonRuntimeDataEmitter  ← Backend.HasFlag(Json)
│       │   └── ModOverlayDataEmitter   ← ModSupport == true
│       ├── ITemplateLiteralRule
│       └── IDcPostProcessor
│
└── ForAttributeWithMetadataName[ModPlugin]
    └── ModPluginRegistrations.g.cs    ← plugin registration
```

### Companion Emitters

After the main emitter (first-match), **all** matching companion emitters run. Each produces a separate file:

| Companion                | Trigger                   | File suffix         | Generates                                                                                   |
| ------------------------ | ------------------------- | ------------------- | ------------------------------------------------------------------------------------------- |
| `SqliteDataEmitter`      | `Backend.HasFlag(Sqlite)` | `.Sqlite.g.cs`      | SQL constants, `IDbCommand` factory, `DbDataReader→T`, lazy `SqlRepository<T>`              |
| `JsonRuntimeDataEmitter` | `Backend.HasFlag(Json)`   | `.JsonRuntime.g.cs` | `Utf8JsonReader→T` reader (no reflection), `LoadAll()`, `JsonRepository<T>`                 |
| `ModOverlayDataEmitter`  | `ModSupport==true`        | `.ModOverlay.g.cs`  | `LoadMods()`, `AddEntry()`, `RemoveEntry()`, `Clear()`, `AddRange()`, adapter notifications |

---

## Generated API — Full Reference

### Per `[CatalystData]` target

**File: `{Type}.DataCatalyst.g.cs`**

```csharp
public enum {Type}Kind { Key1, Key2, ... }

public partial struct {Type} {
    // Column properties
    public {FieldType} {ColumnName} { get; init; }
    public {Type}Kind Kind { get; init; }

    // Static fields — one per row
    public static readonly {Type} {Key} = new() { Kind = ..., ... };

    // FrozenDictionaries
    public static FrozenDictionary<{Type}Kind, {Type}> All;
    public static FrozenDictionary<string, {Type}Kind> KindByName;

    // Properties
    public static int Count => All.Count;
    public static IEnumerable<{Type}Kind> Kinds => All.Keys;
    public static IEnumerable<{Type}> Values => All.Values;

    // Access
    public static {Type} Get({Type}Kind kind);
    public static bool TryGet({Type}Kind kind, out {Type} value);
    public static bool Contains({Type}Kind kind);

    // Enum mapping
    public static {Type}Kind GetKind(string name);
    public static bool TryGetKind(string name, out {Type}Kind kind);

    // Query/Filter
    public static IEnumerable<{Type}> Query(Func<{Type}, bool> predicate);
    public static IEnumerable<{Type}> Find(Func<{Type}, bool> predicate);

    // Repository (game can swap)
    public static IDataRepository<{Type}Kind, {Type}> Repository { get; set; }

    // Query/Filter
    public static IEnumerable<{Type}> Query(Func<{Type}, bool> predicate);
    public static IEnumerable<{Type}> Find(Func<{Type}, bool> predicate);

    // Cross-catalog reference (via RefTo attribute)
    public DataRef<TTarget, TTargetKind>? RefColumn { get; init; }

    // Catalog discovery
    [ModuleInitializer] internal static void RegisterCatalog();
}
```

**When Backend != None**, also:

```csharp
    private sealed class Core{Type}Repository : IDataRepository<{Type}Kind, {Type}>;
    public static IDataRepository<{Type}Kind, {Type}> ResolveRepository();
```

**When ModSupport == true**, `Get`/`TryGet` auto-check `{Type}Mod` overrides.

### Mod overlay

**File: `{Type}.DataCatalyst.ModOverlay.g.cs`**

```csharp
public static partial class {Type}Mod {
    // Loading
    public static void LoadMods(string modDir);  // scans *.json + DSL readers

    // C# injection
    public static void AddEntry(string key, {Type} entry);
    public static void RemoveEntry(string key);
    public static void Clear();
    public static void AddRange(params (string Key, {Type} Value)[] entries);

    // Access
    public static {Type} Get({Type}Kind kind);
    public static IEnumerable<{Type}> GetAllModEntries();

    // Adapter notification — on every operation:
    //   DataViewAdapterRegistry.GetAdapters<{Type}>()
}

// Internal (used by struct's auto-mod-check):
internal static bool TryGet(string name, out {Type} value);
internal static string KindToString({Type}Kind kind);
```

### SQLite

**File: `{Type}.DataCatalyst.Sqlite.g.cs`**

```csharp
public static partial class {Type}Sql {
    public const string TableName = "{Type}";
    public const string SelectAll = "SELECT Kind, Col1, Col2 FROM {Type}";

    public static IDbCommand CreateSelectAllCommand(IDbConnection conn);
    public static {Type} ReadRow(DbDataReader reader);  // ordinal access
}
public sealed class {Type}SqlRepository : IDataRepository<{Type}Kind, {Type}> {
    public {Type}SqlRepository(Func<IDbConnection> connectionFactory);  // lazy, thread-safe
}
```

### JSON runtime

**File: `{Type}.DataCatalyst.JsonRuntime.g.cs`**

```csharp
public static partial class {Type}Json {
    public static {Type} Read(ref Utf8JsonReader reader);    // switch on Kind string → enum
    public static List<{Type}> LoadAll(string filePath);      // array JSON → List<T>
}
public sealed class {Type}JsonRepository : IDataRepository<{Type}Kind, {Type}> {
    public {Type}JsonRepository(string filePath);
}
```

---

### `DataCatalystRuntime.g.cs` (once per compilation)

```csharp
namespace FM39hz.DataCatalyst.Runtime;

// === Data access ===
public interface IDataRepository<TKey, TValue> {
    TValue Get(TKey key);
    bool TryGet(TKey key, out TValue value);
    IEnumerable<TValue> GetAll();
    int Count { get; }
}

public readonly struct DataRef<TTarget, TTargetKind> where TTargetKind : struct {
    public TTargetKind Kind { get; }
    public DataRef(TTargetKind kind) => Kind = kind;
}

// === DSL reader ===
public interface IDslReader<TValue> {
    string FileExtension { get; }
    bool TryRead(string text, out TValue value);
}
public static class DslReaderRegistry { /* thread-safe */ }

// === Engine adapter ===
public interface IDataViewAdapter<T> {
    void OnEntryAdded(string key, T entry);
    void OnEntryRemoved(string key);
    void OnEntryModified(string key, T oldEntry, T newEntry);
    void OnAllCleared();
}
public static class DataViewAdapterRegistry { /* thread-safe */ }

// === Mod plugin ===
public interface IModPlugin {
    string Name { get; }
    string[] Dependencies { get; }
    void OnLoad(IModGameContext context);
}
public interface IModGameContext {
    T? GetService<T>() where T : class;
    void RegisterService<T>(T service) where T : class;
}
public sealed class ModGameContext : IModGameContext { /* delegates to ServiceRegistry */ }
public static class ServiceRegistry { /* thread-safe */ }
public static class PluginRegistry { /* Register + LoadAll with topo sort */ }

// === Backend ===
public static class DataBackendConst {
    public const int None = 0, Json = 1, Sqlite = 2, All = 3;
}
public static class DataBackendSelector { /* Initialize() + Current, lazy+thread-safe */ }

// === Catalog discovery ===
public static class CatalogRegistry {
    public static void Register<T>();        // [ModuleInitializer] per catalog
    public static Type[] GetAll();
}
```

---

## Plugin System

### Generator-time plugin (new shape, primitive, schema source, emitter)

```csharp
[DcPlugin(typeof(IPrimitiveTypeRule))]
public sealed class DecimalPrimitiveRule : IPrimitiveTypeRule {
    [ModuleInitializer]
    internal static void Register() =>
        DcPluginRegistry.Register(new DecimalPrimitiveRule(), typeof(FloatPrimitiveRule));

    public string Name => "decimal";
    public int Rank => 4;
    public bool TryInfer(JsonValueModel value) => /* ... */;
    public string EmitLiteral(JsonValueModel value) => /* ... */;
    public string EmitDefault() => "0m";
}
```

### Companion emitter (runtime code generation)

```csharp
[DcPlugin(typeof(ITypeEmitter))]
public sealed class MyCompanionEmitter : ITypeEmitter {
    [ModuleInitializer]
    internal static void Register() =>
        DcPluginRegistry.RegisterCompanion(new MyCompanionEmitter());

    public string Name => "MyCompanion";
    public bool Applies(DcGenerationContext ctx) => ctx.ModSupport;
    public string Emit(IReadOnlyList<RowData> rows, SchemaInfo schema, DcGenerationContext ctx) {
        // Return C# source; companion file = {Target}.DataCatalyst.{Name}.g.cs
    }
}
```

### DSL reader plugin (runtime data, compile-time registered)

```csharp
public sealed class YamlItemReader : IDslReader<Item> {
    [ModuleInitializer]
    internal static void Register() => DslReaderRegistry.Register(new YamlItemReader());
    public string FileExtension => ".yaml";
    public bool TryRead(string text, out Item value) { /* parse yaml */ }
}
```

### C# mod plugin

```csharp
[ModPlugin("SkillPack", ["CoreLib"])]
public class SkillPack : IModPlugin {
    public void OnLoad(IModGameContext ctx) {
        ItemMod.AddEntry("SuperPotion", new Item { Health = 500 });
        var systems = ctx.GetService<ISystemRegistry>();
        systems?.Register(new CombatLevelSystem());
    }
}
```

### Engine adapter

```csharp
public class FrifloItemAdapter : IDataViewAdapter<Item> {
    private readonly EntityStore _store;

    public void OnEntryAdded(string key, Item entry) {
        var e = _store.CreateEntity();
        e.Add(new ItemWeight { Value = entry.Weight });
        e.Add(new ItemHealth { Value = entry.Health });
    }
    public void OnEntryRemoved(string key) { /* destroy entity */ }
    public void OnEntryModified(string key, Item old, Item @new) { /* update component */ }
    public void OnAllCleared() { /* destroy all mapped entities */ }
}

[ModuleInitializer]
internal static void Register() =>
    DataViewAdapterRegistry.Register<Item>(new FrifloItemAdapter(store));
```

---

## Design Constraints

- Every shape selected by explicit `CanRead`/`Applies` — no heuristics
- Array-of-objects requires `KeyField` — no auto-keying
- Column named `Kind` rejected at compile time — no auto-rename
- Primitive widening via `Rank` — no central table
- Plugin ordering via topo sort over `dependsOn` — no numeric priority
- Companion emitters run in dependency order — same topo sort
- SQLite backend uses `IDbConnection`/`DbDataReader` — no hard dep on `Microsoft.Data.Sqlite`
- JSON reader uses `Utf8JsonReader` with generated switch — no `Enum.Parse`
- All runtime plugin registration via `[ModuleInitializer]` — trimmer sees every type
- Runtime builds contain **no** analyzer code and **no** reflection — AOT/trim safe
