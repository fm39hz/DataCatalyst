# DataCatalyst.SourceGen

Universal Data-Driven Source Generator. Reads JSON files (from `<AdditionalFiles>`) and emits strongly-typed, **reflection-free** static catalogs into any `partial` type marked with `[CatalystData]`.

**Native AOT / trim-safe** — the generated C# uses `FrozenDictionary`, `ImmutableArray`, and plain struct initializers. No reflection, no runtime JSON parsing for core data.

The analyzer ships only during compilation; game binaries contain only the generated C#.

**Reflection policy:** No .NET reflection APIs. Roslyn symbol APIs are compile-time only. `[ModuleInitializer]` here registers plugins in the **compiler** process, not in the player build.

## Goals

1. Compile-time materialization of authored JSON into static code (core catalog)
2. AOT/trimming friendly runtime (no reflection, no runtime JSON parse for core data)
3. Runtime backend switching (JSON ↔ SQLite) — source-generated typed readers
4. Mod content loading — runtime data files merge with core catalog
5. DSL plugin readers — custom text formats via `IDslReader<T>` interface
6. All of the above without string-based public APIs — enum/type access only

## Pipeline

```
UniversalDataGenerator (IIncrementalGenerator)
└── PipelineDriver
    ├── IEntryPointReader      → ObjectOfStrings | ObjectOfObjects | ArrayOfObjects
    ├── ISchemaProvider        → Template | Inference
    ├── IPrimitiveTypeRule     → bool | int | long | float | string | decimal …
    ├── ITypeEmitter (main)    → PartialStruct (struct + enum + FrozenDictionary)  ← FIRST match
    ├── ITypeEmitter (companion) → SqliteDataEmitter | JsonRuntimeDataEmitter | ModOverlayDataEmitter  ← ALL matching
    ├── ITemplateLiteralRule   → template non-primitive literal emitter (enum-safe)
    └── IDcPostProcessor       → (none built-in)
```

### Companion Emitters

After the main emitter runs (first-match), ALL matching **companion emitters** run in dependency order. Each produces a separate source file:

| Companion | Trigger | File | Content |
|---|---|---|---|
| `SqliteDataEmitter` | `Backend.HasFlag(Sqlite)` | `{Type}.DataCatalyst.Sqlite.g.cs` | SQL constants, `SqliteCommand` factory, `SqliteDataReader→T` materializer, `SqlRepository<T>` |
| `JsonRuntimeDataEmitter` | `Backend.HasFlag(Json)` | `{Type}.DataCatalyst.Json.g.cs` | Hand-rolled `Utf8JsonReader→T` reader (no reflection), `LoadAll()`, `JsonRepository<T>` |
| `ModOverlayDataEmitter` | `ModSupport==true` | `{Type}.DataCatalyst.Mod.g.cs` | `LoadMods()` directory scanner, core+mod merge, DSL reader integration |

## Generated API

For each target type `Foo`, DataCatalyst emits:

**Core (always):**
- `FooKind` enum — stable, typed row identities
- `Foo` static row fields (`Foo.Potion`, `Foo.Elixir`, …)
- `All : FrozenDictionary<FooKind, Foo>` — O(1) lookup
- `KindByName : FrozenDictionary<string, FooKind>`
- `Count`, `Kinds`, `Values` properties
- `Get(FooKind)`, `TryGet(FooKind, out Foo)`, `Contains(FooKind)`
- `GetKind(string)`, `TryGetKind(string, out FooKind)` — deterministic string→Kind mapping

**No string-based `Get(name)` / `TryGet(name, out …)` public APIs.** Data access is exclusively through the `FooKind` enum.

**When Backend != None:**
- `Foo.Get(FooKind)` auto-resolves: mod override → runtime backend repo → core FrozenDictionary
- `Foo.TryGet(FooKind, out Foo)` — same chain
- `CoreFooRepository` (private wrapper around core FrozenDictionary)
- `ResolveRepository()` — returns `IDataRepository<FooKind, Foo>` for the active backend

**When ModSupport == true:**
- `Foo.Get(FooKind)` checks `FooMod.TryGet()` first — mod-overridden entries return automatically
- `FooMod.LoadMods(string modDir)` — scans `.json` + registered DSL extensions
- `FooMod.Get(FooKind)` — explicit mod-or-core access

## Attribute

```csharp
[CatalystData(string jsonPath, string entryPoint = "", Type templateType = null)]
```

Named parameters:

| Parameter | Type | Default | Description |
|---|---|---|---|
| `JsonPath` | `string` | (required) | Path to JSON file in `<AdditionalFiles>` |
| `EntryPoint` | `string` | `""` (root) | Sub-property path in the JSON document |
| `TemplateType` | `Type` | `null` | CLR type whose shape constrains the inferred schema |
| `KeyField` | `string` | `""` | Required for `ArrayOfObjects` reader — names the JSON property used as row key |
| **`Backend`** | `int` (cast from `DataBackend`) | `0` (None) | Which runtime backends to generate: `None=0`, `Json=1`, `Sqlite=2`, `All=3` |
| **`ModSupport`** | `bool` | `false` | If `true`, generates mod overlay + JSON runtime reader (auto-includes `Backend.Json`) |

```csharp
// Core only
[CatalystData("items.json")]
public partial struct Item { }

// Core + JSON runtime + SQLite + mod overlay
[CatalystData("items.json", Backend = 3, ModSupport = true)]
public partial struct Item { }

// Shorthand: Backend = DataBackend.All (via generated enum)
[CatalystData("items.json", Backend = DataBackend.All, ModSupport = true)]
public partial struct Item { }
```

## Architecture

### Plugin System

Plugins self-register through `[ModuleInitializer]` calling `DcPluginRegistry.Register(plugin, dependsOn: ...)`. The registry resolves a deterministic dependency-topological order per contract (Kahn's algorithm, cycle-safe). For the main pipeline, the driver picks the **first** plugin whose `CanRead`/`Applies` returns `true`. For companion emitters, **all** matching plugins run.

| Contract | Registration method | Pipeline phase |
|---|---|---|
| `IEntryPointReader` | `Register(IEntryPointReader)` | First match |
| `ISchemaProvider` | `Register(ISchemaProvider)` | First match |
| `IPrimitiveTypeRule` | `Register(IPrimitiveTypeRule)` | All registered (ordered by Rank) |
| `ITypeEmitter` (main) | `Register(ITypeEmitter)` | First match |
| `ITypeEmitter` (companion) | `RegisterCompanion(ITypeEmitter)` | **All** matching |
| `IDcPostProcessor` | `Register(IDcPostProcessor)` | All registered |
| `ITemplateLiteralRule` | `Register(ITemplateLiteralRule)` | All registered |

### Runtime Abstractions (emitted in `DataCatalystRuntime.g.cs`)

```csharp
namespace FM39hz.DataCatalyst.Runtime;

public interface IDataRepository<TKey, TValue> {
    TValue Get(TKey key);
    bool TryGet(TKey key, out TValue value);
    IEnumerable<TValue> GetAll();
    int Count { get; }
}

public interface IDslReader<TValue> {
    string FileExtension { get; }
    bool TryRead(string text, out TValue value);
}

public static class DslReaderRegistry {
    public static void Register<TValue>(IDslReader<TValue> reader);
    public static IEnumerable<IDslReader<TValue>> GetReaders<TValue>();
}

public static class DataBackendSelector {
    public static void Initialize(string? backendOverride = null);
    public static DataBackend Current { get; }
}
```

### Folder layout

```
UniversalDataGenerator.cs   Roslyn-side shell
DcConstants.cs              attribute metadata names
DcDiagnostics.cs            DC0001–DC0016 descriptors
Polyfills.cs                IsExternalInit + ModuleInitializerAttribute (netstandard2.0)

Abstractions/
  Contracts/        public plugin interfaces
  Runtime/          DataBackend, DcGenerationContext
  Schema/           SchemaInfo, SchemaColumn, SchemaType, RowData, JsonValueModel
  Templates/        TemplateMember + ITemplateMetadata

Core/
  PipelineDriver.cs     orchestrates one generation run
  DcPluginRegistry.cs   static registry + topological sort
  TargetInfo.cs         Roslyn symbol → extracted data
  IdentifierGuard.cs    C# identifier validation

Plugins/
  Readers/          ObjectOfStringsReader, ObjectOfObjectsReader, ArrayOfObjectsReader
  Primitives/       BoolPrimitiveRule, IntPrimitiveRule, LongPrimitiveRule,
                    FloatPrimitiveRule, StringPrimitiveRule
  Schema/           InferenceSchemaProvider, TemplateSchemaProvider
  Emitters/         PartialStructEmitter (main)
                    SqliteDataEmitter (companion)
                    JsonRuntimeDataEmitter (companion)
                    ModOverlayDataEmitter (companion)
  Literals/         EnumTemplateLiteralRule
```

## Authoring a Plugin

### Generator-time plugin (reader, schema, primitive, main emitter, post-processor)

```csharp
[DcPlugin(typeof(IPrimitiveTypeRule))]
public sealed class DecimalPrimitiveRule : IPrimitiveTypeRule {
    [ModuleInitializer]
    internal static void Register() =>
        DcPluginRegistry.Register(new DecimalPrimitiveRule(), typeof(FloatPrimitiveRule));

    public string Name => "decimal";
    public int Rank => 4;
    public JsonValueKind BoundKind => JsonValueKind.Number;
    public bool TryInfer(JsonValueModel value) => /* … */;
    public string EmitLiteral(JsonValueModel value) => /* … */;
    public string EmitDefault() => "0m";
}
```

### Companion emitter (runtime code generation)

```csharp
[DcPlugin(typeof(ITypeEmitter))]
public sealed class MyRuntimeEmitter : ITypeEmitter {
    [ModuleInitializer]
    internal static void Register() =>
        DcPluginRegistry.RegisterCompanion(new MyRuntimeEmitter());

    public string Name => "MyRuntime";
    public bool Applies(DcGenerationContext ctx) => ctx.Backend.HasFlag(DataBackend.Json);
    public string Emit(IReadOnlyList<RowData> rows, SchemaInfo schema, DcGenerationContext ctx) {
        // Return generated C# code; companion file will be named {Target}.DataCatalyst.{Name}.g.cs
    }
}
```

### Runtime DSL reader (loaded at runtime, registered at compile time)

```csharp
public sealed class YamlItemReader : IDslReader<Item> {
    [ModuleInitializer]
    internal static void Register() =>
        DslReaderRegistry.Register(new YamlItemReader());

    public string FileExtension => ".yaml";
    public bool TryRead(string text, out Item value) { /* parse yaml, set Kind to existing enum */ }
}
```

`[ModuleInitializer]` registration means the reader type is statically known to the trimmer — AOT-safe.

## Supported Entry-Point Shapes (built-in)

| Shape | Reader | Notes |
|---|---|---|
| object-of-strings | `ObjectOfStringsReader` | Single synthetic `value` column. |
| object-of-objects | `ObjectOfObjectsReader` | Default for object roots. Property names → enum members. |
| array-of-objects | `ArrayOfObjectsReader` | Requires `KeyField` in `[CatalystData]`. |

## Diagnostics

| ID | Severity | Trigger |
|---|---|---|
| DC0001 | Error | JSON file not found in `<AdditionalFiles>` |
| DC0002 | Error | JSON failed to parse |
| DC0003 | Error | Named `EntryPoint` missing in document |
| DC0004 | Error | Entry-point shape unsupported by matched reader |
| DC0005 | Error | Target type missing `partial` modifier |
| DC0006 | Warning | JSON column has no member on supplied template type |
| DC0007 | Error | Inference fell over (mixed shapes, empty array, leading null …) |
| DC0008 | Error | Row key is not a valid C# identifier |
| DC0009 | Error | Array entry-point contains non-object item |
| DC0010 | Error | Array entry-point used without `KeyField` |
| DC0011 | Error | Column member name resolves to `Kind` (collides with synthetic enum property) |
| DC0012 | Error | No `IEntryPointReader` accepted the entry-point |
| DC0013 | Error | No `ISchemaProvider` matched the target |
| DC0014 | Error | No `ITypeEmitter` matched the target |
| **DC0015** | **Error** | `Backend=Sqlite` with nested columns (object/array) — flat schema required |
| **DC0016** | **Error** | `Backend=Json` with nested columns — flat schema required |

## Strict-contract Checklist

- Every shape selected by explicit `CanRead`/`Applies` check; **no heuristics**.
- Array-of-objects rows MUST declare a key column via `KeyField`; **no auto-keying**.
- A JSON column literally named `Kind` is rejected at compile time; **no auto-rename**.
- Primitive widening: each rule owns its own `Rank` — no central table.
- Plugin ordering: deterministic topo sort over declared `dependsOn` — no numeric priority hacks.
- Companion emitters run in dependency order — same topo sort as main pipeline.
- Generated repositories use ordinal-indexed `SqliteDataReader.GetXxx(n)` — no reflection.
- Generated JSON readers use `Utf8JsonReader` with compile-time-known property switches — no `Enum.Parse`.
- DSL readers registered at compile time via `[ModuleInitializer]` — trimmer sees every type.
- Runtime builds contain **no** analyzer code and **no** reflection — AOT / trim safe.
