# FM39hz.DataCatalyst

A Roslyn source generator that bakes JSON into strongly-typed static C# catalogs — with **runtime backend switching**, **mod content support**, and **DSL plugin readers**. All AOT-safe, zero reflection.

## Why this?

An opinionated data-loading solution for games that need flexibility without sacrificing NativeAOT.

- **AOT/Trimming Safe:** Source-generated readers, no reflection.
- **Zero Runtime Overhead (core):** Data baked into `FrozenDictionary` at compile time.
- **Runtime Backend Switch:** Same binary reads from JSON (dev) or SQLite (prod) — config/env select.
- **Mod Content:** Runtime JSON/DSL files override or extend core data — no recompile.
- **Compile-time Safety:** If JSON structure breaks, build fails.

## Setup

### 1. Project Configuration

Add JSON files as `AdditionalFiles` in `.csproj`:

```xml
<ItemGroup>
  <AdditionalFiles Include="Data\*.json" />
</ItemGroup>
```

### 2. Define the Catalog

```csharp
using FM39hz.DataCatalyst;

// Core only (compile-time FrozenDictionary)
[CatalystData("Data/Items.json")]
public partial struct Item { }

// Core + JSON runtime reader + SQLite reader
[CatalystData("Data/Items.json", Backend = DataBackend.All)]
public partial struct Item { }

// Core + JSON runtime + mod overlay support
[CatalystData("Data/Items.json", ModSupport = true)]
public partial struct Item { }

// Everything: core + JSON + SQLite + mods + DSL
[CatalystData("Data/Items.json", Backend = DataBackend.All, ModSupport = true)]
public partial struct Item { }
```

## Example

### Input (`Items.json`)

```json
{
 "Potion": { "Health": 50, "Weight": 0.5 },
 "Elixir": { "Health": 200, "Weight": 0.3 }
}
```

### Output API — Type-Safe, No Strings

Generated enum + struct + static fields + helpers:

```csharp
// Direct access — compile-time known entries
var hp = Item.Potion.Health;

// Enum-based lookup — O(1) FrozenDictionary
var item = Item.Get(ItemKind.Elixir);

// Auto-resolve: mod override → runtime backend → core
// (works with any combination of backends/mods)
var item = Item.Get(ItemKind.Potion);

// Enum-to-name converter (deterministic, for external data)
if (Item.TryGetKind("Potion", out var kind)) {
    var potion = Item.Get(kind);
}

// List all
var all = Item.Values;
```

**No string-based `Get(name)` / `TryGet(name, out ...)` public APIs.** All access is through `ItemKind` enum.

## Runtime Backend Switching

Enable with `Backend = DataBackend.All` (or `Json` / `Sqlite`).

```csharp
// At startup — reads env var "DATACATALYST_BACKEND"
DataBackendSelector.Initialize();

// Or override explicitly:
DataBackendSelector.Initialize("sqlite");

// Item.Get() auto-resolves: backend repo → core FrozenDictionary
var item = Item.Get(ItemKind.Potion);
```

Backend value comes from `DATACATALYST_BACKEND` env var:
- `json` → JSON runtime reader (generated `ItemJson.LoadAll(file)`)
- `sqlite` → SQLite reader (generated `ItemSqlRepository(connectionString)`)
- unset / other → core FrozenDictionary (zero runtime overhead)

Repositories implement `IDataRepository<ItemKind, Item>` for manual use:

```csharp
var repo = new ItemSqlRepository("Data Source=game.db");
var item = repo.Get(ItemKind.Potion);
```

## Mod Content (Data Files)

Enable with `ModSupport = true`.

### Runtime JSON mods

Place `.json` files in a mod directory. Each JSON array entry must include a `"Kind"` string:

```json
[
  { "Kind": "Potion", "Health": 100, "Weight": 0.4 },
  { "Kind": "SuperElixir", "Health": 500, "Weight": 0.2 }
]
```

```csharp
ItemMod.LoadMods("mods/items/");

// Kind known at compile time → mod overrides core
var buffed = Item.Get(ItemKind.Potion); // Health = 100 (mod)

// Mod-only entries accessible through the same enum-based API
// (they must match a known enum value)
```

### DSL Readers

Implement `IDslReader<T>` for custom text formats:

```csharp
public sealed class CsvItemReader : IDslReader<Item> {
    [ModuleInitializer]
    internal static void Register() =>
        DslReaderRegistry.Register(new CsvItemReader());

    public string FileExtension => ".csv";

    public bool TryRead(string text, out Item value) {
        // Parse CSV → set value.Kind, value.Health, ...
        // Must set Kind to an existing ItemKind value
    }
}
```

DSL readers are discovered at compile time (`[ModuleInitializer]`) — AOT-safe, no runtime reflection. They're invoked automatically by `ItemMod.LoadMods()`.

## SQLite Backend

Requires `Backend = DataBackend.Sqlite` or `DataBackend.All`. Generates:

- SQL constants: `ItemSql.TableName`, `ItemSql.SelectAll`
- `ItemSql.CreateSelectAllCommand(SqliteConnection)` — typed command factory
- `ItemSql.ReadRow(SqliteDataReader)` — ordinal-indexed, typed materializer
- `ItemSqlRepository` — `IDataRepository<ItemKind, Item>` implementation

SQLite requires a flat schema (no nested objects/arrays). Emits diagnostic DC0015 if violated.

## Complete Feature Table

| Backend flag | `ModSupport` | Generated components |
|---|---|---|
| `None` (default) | `false` | Core struct + enum + FrozenDictionary |
| `Json` | `false` | Core + `ItemJson` (Utf8JsonReader) + `ItemJsonRepository` |
| `Sqlite` | `false` | Core + `ItemSql` (SQL constants + reader) + `ItemSqlRepository` |
| `All` | `false` | Core + JSON + SQLite |
| any | `true` | Core + JSON + `ItemMod` (overlay + DSL) |
| `All` | `true` | Core + JSON + SQLite + Mod |

## License

MIT — see LICENSE file.
