# FM39hz.DataCatalyst.SourceGen

Universal Data-Driven Source Generator (DataCatalyst). It reads JSON files declared as
`<AdditionalFiles>` and emits strongly-typed, **reflection-free** static catalogs
into any `partial` type marked with `CatalystData`.

**Why it exists (order of goals):** keep the **game** ready for **Native AOT**
and trimming — static, linkable tables with **no runtime `System.Reflection`**
and no runtime JSON loading for those definitions. “Data-driven” authoring
(JSON in source) is how designers work; **compile-time** materialization is how
the runtime stays AOT-safe. The analyzer does not ship inside the game; only
its **generated C#** does.

**Reflection policy:** This project does not use .NET reflection APIs. Roslyn
symbol APIs in source generators are compile-time only and are not a substitute
for runtime reflection in game code. `[ModuleInitializer]` here registers
plugins in the **compiler** when the analyzer loads; it is not a runtime
discovery mechanism in the player build.

The assembly ships as a Roslyn analyzer; consumers reference it with
`OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`.

## Goals

- Compile-time materialization of authored JSON into static code
- AOT/trimming friendly runtime (no reflection, no runtime JSON parse)
- Convenient generated APIs without sacrificing hot-path performance

## Generated API (Convenient + Fast)

For each target type `Foo`, DataCatalyst emits:

- `FooKind` enum for stable, typed row identities
- `Foo` static row fields (`Foo.RowA`, `Foo.RowB`, ...)
- `All : FrozenDictionary<FooKind, Foo>`
- `KindByName : FrozenDictionary<string, FooKind>` (ordinal comparer)
- Convenience helpers:
  - `Get(FooKind kind)`, `TryGet(FooKind kind, out Foo value)`, `Contains(FooKind kind)`
  - `GetKind(string name)`, `TryGetKind(string name, out FooKind kind)`, `Contains(string name)`
  - `Get(string name)`, `TryGet(string name, out Foo value)`
  - `Count`, `Kinds`, `Values`

This gives ergonomic key-based access while keeping lookups O(1) and allocation-free
on steady-state read paths.

## Architecture

DataCatalyst is a thin pipeline driver orchestrating five plugin contracts. The
generator class itself is a 49-line shim — every behaviour lives in a plugin.

```
UniversalDataGenerator (Roslyn IIncrementalGenerator)
└── PipelineDriver
    ├── IEntryPointReader      → ObjectOfStrings | ObjectOfObjects | ArrayOfObjects
    ├── ISchemaProvider        → Template | Inference
    ├── IPrimitiveTypeRule     → bool | int | long | float | string  (rank-ordered)
    ├── ITypeEmitter           → PartialStruct (default)
    ├── ITemplateLiteralRule   → template non-primitive literal emitter (enum-safe)
    └── IDcPostProcessor    → (none built-in)
```

Plugins self-register through `[ModuleInitializer]` calling
`DcPluginRegistry.Register(plugin, dependsOn: ...)`. The registry resolves a
deterministic dependency-topological order per contract (cycle-safe), then the
driver picks the first plugin whose `CanRead`/`Applies` returns `true`. Adding
a primitive, shape, schema source, or emission strategy is a single new class
— **zero edits** to anything in `Core/`. This is the OCP contract.
Template-bound non-primitive literals are also pluginized through
`ITemplateLiteralRule`, so the default emitter has no enum-specific hardcode.

## Folder layout

```
Abstractions/   public plugin contracts + data types (RowData, SchemaInfo, …)
Core/           PipelineDriver, DcPluginRegistry, TargetInfo
Plugins/
  Readers/      built-in entry-point readers
  Primitives/   built-in primitive type rules
  Schema/       built-in schema providers
  Emitters/     built-in type emitters
UniversalDataGenerator.cs   Roslyn-side shell
DcConstants.cs           attribute metadata names
DcDiagnostics.cs         DC0001–DC0014 descriptors
Polyfills.cs                IsExternalInit + ModuleInitializerAttribute (netstandard2.0)
```

## Authoring a plugin

A custom plugin is just a class that:

1. Implements one of the contracts in `Abstractions/`.
2. Tags itself with `[DcPlugin(typeof(IContract))]` (documentation only).
3. Calls `DcPluginRegistry.Register(...)` from a `[ModuleInitializer]`-tagged
   static method, optionally passing dependency plugin types.

```csharp
public sealed class DecimalPrimitiveRule : IPrimitiveTypeRule {
    [ModuleInitializer]
    internal static void Register() => DcPluginRegistry.Register(new DecimalPrimitiveRule(), typeof(FloatPrimitiveRule));

    public string Name => "decimal";
    public int Rank => 4;
    public JsonValueKind BoundKind => JsonValueKind.Number;
    public bool TryInfer(JsonValueModel value) => /* … */;
    public string EmitLiteral(JsonValueModel value) => /* … */;
    public string EmitDefault() => "0m";
}
```

`FM39hz.DataCatalyst.Test/Unit/DecimalPrimitivePluginTests.cs` is the
live OCP regression test for this story.

## Supported entry-point shapes (built-in)

| Shape              | Reader                  | Notes                                                                                  |
| ------------------ | ----------------------- | -------------------------------------------------------------------------------------- |
| object-of-strings  | `ObjectOfStringsReader` | Single synthetic `value` column. Selected when every property is a JSON string.        |
| object-of-objects  | `ObjectOfObjectsReader` | Default for object roots. Property names become enum members.                          |
| array-of-objects   | `ArrayOfObjectsReader`  | Requires `KeyField`; declared in `[CatalystData(..., KeyField = "id")]`.           |

## Diagnostics

| ID         | Severity | Trigger                                                                                              |
| ---------- | -------- | ---------------------------------------------------------------------------------------------------- |
| ID     | Severity | Trigger                                                                                              |
| ------ | -------- | ---------------------------------------------------------------------------------------------------- |
| DC0001 | Error    | JSON file not found in `<AdditionalFiles>`.                                                          |
| DC0002 | Error    | JSON failed to parse.                                                                                |
| DC0003 | Error    | Named `EntryPoint` missing in document.                                                              |
| DC0004 | Error    | Entry-point shape unsupported by the matched reader.                                                 |
| DC0005 | Error    | Target type missing the `partial` modifier.                                                          |
| DC0006 | Warning  | JSON column has no member on the supplied template type.                                             |
| DC0007 | Error    | Inference fell over (mixed shapes, empty array, leading null ...).                                  |
| DC0008 | Error    | Row key/identifier rejected (must be a non-empty C# identifier).                                     |
| DC0009 | Error    | Array entry-point contains a non-object item.                                                        |
| DC0010 | Error    | Array entry-point used without `KeyField`.                                                           |
| DC0011 | Error    | Column member name resolves to `Kind` (collides with the synthetic enum property).                  |
| DC0012 | Error    | No `IEntryPointReader` accepted the entry-point.                                                     |
| DC0013 | Error    | No `ISchemaProvider` matched the target.                                                             |
| DC0014 | Error    | No `ITypeEmitter` matched the target.                                                                |

## Strict-contract checklist (no workarounds)

- Every shape is selected by an explicit `CanRead`/`Applies` check; **no heuristics**.
- Array-of-objects rows MUST declare an `id`-style column; **no auto-keying**.
- A JSON column literally named `Kind` is rejected at compile time; **no auto-rename**.
- The primitive widening table lives nowhere — each rule owns its own `Rank`.
- Plugins discover each other through `[ModuleInitializer]` and deterministic
  topo ordering over declared dependencies; **no numeric priority hardcode** and
  **no central manifest**.
Runtime builds contain **no** analyzer code and **no** reflection for these tables
— only generated static data (AOT / trim safe).
