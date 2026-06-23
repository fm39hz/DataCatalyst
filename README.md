# DataCatalyst

[![NuGet Version](https://img.shields.io/nuget/v/DataCatalyst?style=flat-square)](https://www.nuget.org/packages/DataCatalyst/)
[![CI Status](https://img.shields.io/github/actions/workflow/status/fm39hz/DataCatalyst/ci.yml?branch=master&style=flat-square)](https://github.com/fm39hz/DataCatalyst/actions)
[![License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](LICENSE)

**DataCatalyst** is a concept composition framework for C#/.NET. Data is the Single Source of Truth. JSON data files define game content, and SourceGen generates everything else.

---

> **Code itself has no content.** Game logic, behaviors, values, etc... should never be hardcoded. Designers parameterize everything to model the world.

---

## 🚀 Quick Start

```bash
dotnet add package DataCatalyst
dotnet add package DataCatalyst.Loaders.Json
```

### 1. Write Data

`Data/Goblin.json`:

```json
{
	"concept": "Enemy",
	"health": { "current": 50, "max": 50 },
	"combatStats": { "attackPower": 8, "defense": 5 }
}
```

SourceGen generates component structs + entry constants automatically.

### 2. Load, Resolve, Access

```csharp
// Simple fluent API
var catalog = new DataPipeline()
    .Load(new JsonDataLoader(), "Data/")
    .Load(new JsonDataLoader(), "Mods/")
    .Build();

// Which is load under the hood
var data  = JsonDataLoader.LoadDirectory("Data/");
var mods  = JsonDataLoader.LoadDirectory("Mods/");
var result = data.Concat(mods);
var graph   = DataGraphBuilder.Build(result.Entries);
var catalog = DataCatalogBuilder.Resolve(graph);

// Access
var hp  = catalog.Get<Health>(Concept.Enemy.Goblin);
var atk = catalog.Get<CombatStats>(Concept.Enemy.Goblin);
```

### 3. With Concepts

```json
// concepts.json
{ "Enemy": { "description": "Hostile entities" } }
```

```csharp
var hp = catalog.Get<Health>(Concept.Enemy.Goblin);
var enemies = catalog.GetConcept<Concept.Enemy>();
enemies.Get<Health>(Concept.Enemy.Goblin);
```

`Concept.Enemy.Goblin` is `const int` - compile-time safe

---

## 📦 Packages

```bash
dotnet add package DataCatalyst                              # SourceGen + Core
dotnet add package DataCatalyst.Loaders.Json                  # JSON loader
dotnet add package DataCatalyst.Extensions                    # Compare, Composition, Materialization
dotnet add package DataCatalyst.Plugins.StateEngine
dotnet add package DataCatalyst.Plugins.StateEngine.SourceGen
```

SourceGen packages as analyzers:

```xml
<PackageReference Include="..." OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

---

## 🧩 Usage

### DataCatalog

Pipeline final result

```csharp
catalog.Get<Health>(Concept.Enemy.Goblin).Current
catalog.TryGet<Element>(Concept.Enemy.FireDragon, out var e)
catalog.GetEntry(Concept.Enemy.Goblin).SourceFile

// Concept-scoped view
var enemies = catalog.GetConcept<Concept.Enemy>();
enemies.Get<Health>(Concept.Enemy.Goblin).Current
```

### Loader

Implement `IDataLoader` to use any format (CSV, MsgPack, ...).

```csharp
public class CsvDataLoader : IDataLoader {
    public LoadResult LoadDirectory(string path) {
        foreach (var file in Directory.GetFiles(path, "*.csv"))
            // Parse CSV → DataEntry with typed components
    }
    public LoadResult LoadFile(string path) => LoadDirectory(Path.GetDirectoryName(path));
}
```

### Plugin

Plugin is your specialized use case for your game that hook into pipeline

```csharp
[DataPlugin(DependsOn = [typeof(GameConceptPlugin)])]
public class MyPlugin : ICatalogPlugin {
    public bool IsEnabled => true;
    public void OnLoad() => env.Primitives.Register<MyComponent>();
    public void OnCatalogResolved(DataCatalog catalog, List<string> diags) {
        foreach (var entry in catalog.Entries.Values)
            if (entry.TryGet<MyComponent>(out var c))
                Process(c);
    }
}
```

Pipeline hooks: `IPostLoadPlugin` (after load) → `IGraphPlugin` (after graph) → `ICatalogPlugin` (after catalog).

### Materializer

Bridge from DataCatalyst's component sang engine-specific object (GameObject, Node, ...).

```csharp
var mat = new DataMaterializer<GameObject>();
mat.Register<Health>((go, h) => go.GetComponent<HealthBar>().SetMax(h.Max));
mat.Register<AttackPower>((go, a) => go.GetComponent<DamageDealer>().Power = a.Value);

var goblin = YourGoblinIntialSetupFunctionHere();
mat.Materialize(catalog.GetEntry(Concept.Enemy.Goblin), goblin);
```

### Extensions

Custom per-source config - per-loader namespace, injected attributes (Type-safe, no strings).

```csharp
// Game data - additional attribute here
[assembly: DataCatalystConfig("Data/", Namespace = "Game.Components",
    Attributes = new[] { typeof(YourAttribute) })]

// Mod data - different namespace, different attribute
[assembly: DataCatalystConfig("Mods/", Namespace = "Mod.Components",
    Attributes = new[] { typeof(YourAnotherAttribute) })]
```

SourceGen generates separate namespaces per source:

```csharp
// Data/ → Game.Components
namespace Game.Components;
[IComponent] public partial struct Health { int Current; int Max; }

// Mods/ → Mod.Components
namespace Mod.Components;
[IComponent] public partial struct Health { int Current; int Max; }
```

| Namespace                                 | Types                                                                              |
| ----------------------------------------- | ---------------------------------------------------------------------------------- |
| `DataCatalyst.Extensions.Compare`         | `CompareOp`, `OperatorParser`                                                      |
| `DataCatalyst.Extensions.Composition`     | `TransitionDef`, `ConditionGroupDef`, `SensorConditionDef`, `SensorInfluenceDef`   |
| `DataCatalyst.Extensions.Materialization` | `DataMaterializer<T>`, `ComponentMaterializer<TC,TT>`, `IComponentMaterializer<T>` |

---

## 🔌 Bundled Plugin

### StateEngine

Data-driven hierarchical FSM. States and signals are concepts - nothing special.

```json
{
	"concept": "Locomotion",
	"defaultState": "Idle",
	"states": {
		"idle": {
			"transitions": [
				{
					"targetState": "Walk",
					"priority": 5,
					"conditions": {
						"all": [
							{
								"signal": "PlayerDistance",
								"op": "<",
								"value": 20
							}
						]
					}
				}
			]
		},
		"walk": {},
		"chase": {}
	}
}
```

```json
{ "concept": "AISensor", "defaultValue": 999 }
```

```csharp
var locomotion = catalog.Get<StateGroup>(Concept.Locomotion.Locomotion);
var baked = StateEngineBaker.Bake(locomotion, catalog);

var result = StateEngineEvaluator.Evaluate(
    baked.DefaultStateId, baked, viableStates,
    signalId => signalId switch {
        Concept.AISensor.PlayerDistance => entity.DistanceToPlayer,
        _ => 0f
    });
```

StateEngine is originally designed for ECS: ONE system evaluates ALL entities, but it is compat with normal use-case

---

## ⚖️ License

Distributed under the MIT License. See [LICENSE](LICENSE).
