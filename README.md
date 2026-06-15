# ⚡ DataCatalyst

[![CI][ci-badge]][ci] [![NuGet][nuget-badge]][nuget] [![License][license-badge]][license]

Roslyn source generator: **JSON → strongly-typed C# struct + enum + FrozenDictionary** at compile time. Zero reflection. NativeAOT-safe. Engine-agnostic.

---

Data-driven definitions are compile-time materialized; runtime flexibility (backend switching, mod loading, C# plugins) goes through generated contracts — never through reflection or runtime JSON parsing for core data.

```
dotnet add package FM39hz.DataCatalyst --version 0.2.0
```

## 💡 Example

```csharp
[CatalystData("Items.json")]
public partial struct Item { }

// Compile-time baked
var hp = Item.Potion.Health;
var item = Item.Get(ItemKind.Elixir);
var heavy = Item.Query(x => x.Weight > 1f);

// Deterministic enum mapping
Item.TryGetKind("Potion", out var kind);
```

## 🔗 Backend Switching

```csharp
[CatalystData("Items.json", Backend = DataBackendConst.All)]
public partial struct Item { }
```

**SQLite** — lazy, no hard dependency on Microsoft.Data.Sqlite (uses `IDbConnection`/`DbDataReader`):

```csharp
using var conn = new SqliteConnection("Data Source=game.db");
var cmd = ItemSql.CreateSelectAllCommand(conn);
var repo = new ItemSqlRepository(() => new SqliteConnection("..."));
```

**JSON** — `Utf8JsonReader`, no reflection:

```csharp
var item = ItemJson.Read(ref reader);
var items = ItemJson.LoadAll("items.json");
var repo = new ItemJsonRepository("items.json");
```

**Env-based** — `DATACATALYST_BACKEND`, `DATACATALYST_CONNECTION`, `DATACATALYST_DATAPATH`.

## 💾 Mod Content

Enable with `ModSupport = true`:

```csharp
[CatalystData("Items.json", ModSupport = true)]
public partial struct Item { }

ItemMod.LoadMods("mods/items/");               // JSON or DSL files
ItemMod.AddEntry("SuperPotion", new() { Health = 999 });  // C# injection
ItemMod.RemoveEntry("OldPotion");

var item = Item.Get(ItemKind.Potion);  // mod override → core fallback
```

**Engine adapter** — one-time bridge per engine, auto-notified on changes:

```csharp
public class GodotItemAdapter : IDataViewAdapter<Item> {
    public void OnEntryAdded(string key, Item entry) => _parent.AddChild(new Node { Name = key });
    public void OnEntryRemoved(string key) { /* ... */ }
    public void OnEntryModified(string key, Item old, Item @new) { /* ... */ }
    public void OnAllCleared() { /* ... */ }
}
```

**Custom DSL reader** — YAML, TOML, or any text format:

```csharp
public sealed class YamlItemReader : IDslReader<Item> {
    public string FileExtension => ".yaml";
    public bool TryRead(string text, out Item value) { /* ... */ }
}
```

## 🧩 C# Mod Plugins

NativeAOT-safe. No `Assembly.Load`, no `Activator.CreateInstance`.

```csharp
[ModPlugin("SkillPack", ["CoreLib"])]
public class SkillPack : IModPlugin {
    public void OnLoad(IModGameContext ctx) {
        ItemMod.AddEntry("SuperPotion", new() { Health = 500 });
        ctx.GetService<ISystemRegistry>()?.Register(new CombatLevelSystem());
    }
}
```

**Auto-registered** via `[ModuleInitializer]` — just compile together.

## 🔗 Cross-Catalog References

```csharp
public struct ItemTemplate {
    public int Health { get; init; }
    public DataRef<Buff, BuffKind> BuffRef { get; init; }
}

[CatalystData("items.json", typeof(ItemTemplate))]
public partial struct Item { }

var buff = Buff.Get(item.BuffRef.Kind);
```

## 🗄️ Generated API

| Member                                  | Description                    |
| --------------------------------------- | ------------------------------ |
| `Item.Potion`                           | Static field per JSON entry    |
| `Item.Get(ItemKind)`                    | O(1) `FrozenDictionary` lookup |
| `Item.Query(predicate)`                 | Filter entries                 |
| `Item.TryGetKind(string, out ItemKind)` | Deterministic string→enum      |
| `Item.Repository { get; set; }`         | Swappable `IDataRepository`    |
| `ItemMod.AddEntry(key, value)`          | Mod data injection             |
| `ItemSql.CreateSelectAllCommand(conn)`  | SQLite `IDbCommand` factory    |

**Always emitted** (once per compilation): `DataCatalystRuntime.g.cs` + `CatalystDataAttribute.g.cs`

## 📦 Generated Components

| Backend  | ModSupport | Files                                           |
| -------- | ---------- | ----------------------------------------------- |
| `None`   | `false`    | Core struct + enum + `FrozenDictionary`         |
| `Json`   | `false`    | Core + `ItemJson` + `ItemJsonRepository`        |
| `Sqlite` | `false`    | Core + `ItemSql` + `ItemSqlRepository`          |
| `All`    | `false`    | Core + JSON + SQLite                            |
| any      | `true`     | Core + JSON + `ItemMod` + adapter notifications |

## 🔌 Extension Points

| Extension        | Interface                                 | Registry                  |
| ---------------- | ----------------------------------------- | ------------------------- |
| Engine adapter   | `IDataViewAdapter<T>`                     | `DataViewAdapterRegistry` |
| DSL reader       | `IDslReader<T>`                           | `DslReaderRegistry`       |
| C# mod plugin    | `IModPlugin`                              | `PluginRegistry`          |
| Custom backend   | `IDataRepository<TKey, TValue>`           | `Item.Repository = ...`   |
| Generator plugin | `IPrimitiveTypeRule`, `ITypeEmitter`, ... | `DcPluginRegistry`        |

---

## License

MIT

[ci-badge]: https://img.shields.io/github/actions/workflow/status/fm39hz/DataCatalyst/ci.yml?branch=main&style=flat-square
[ci]: https://github.com/fm39hz/DataCatalyst/actions/workflows/ci.yml
[nuget-badge]: https://img.shields.io/nuget/v/FM39hz.DataCatalyst?style=flat-square
[nuget]: https://www.nuget.org/packages/FM39hz.DataCatalyst
[license-badge]: https://img.shields.io/github/license/fm39hz/DataCatalyst?style=flat-square
[license]: https://github.com/fm39hz/DataCatalyst/blob/main/LICENSE
