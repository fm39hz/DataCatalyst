# DataCatalyst

[![NuGet Version](https://img.shields.io/nuget/v/DataCatalyst?style=flat-square)](https://www.nuget.org/packages/DataCatalyst/)
[![CI Status](https://img.shields.io/github/actions/workflow/status/fm39hz/DataCatalyst/ci.yml?branch=master&style=flat-square)](https://github.com/fm39hz/DataCatalyst/actions)
[![License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](LICENSE)

**DataCatalyst** is a concept composer framework for C#/.NET

---

> **Code itself has no content.** Game logic, behaviors, values, etc... should never be hardcoded. Designers parameterize everything to model the world.

---

## 🚀 Quick Start

```bash
dotnet add package DataCatalyst
dotnet add package DataCatalyst.Loaders.Json
```

### 1. Write Data

`Data/Creatures.json`:

```json
{
	"hero": {
		"concept": ["Creature", "Player", "Protagonist"],
		"health": { "initial": 50, "max": 50 },
		"combatStats": { "baseDamage": 8, "baseDefense": 5 }
	}
}
```

### 2. With Concept & Aspect

```csharp
[GameConcept]
public record struct Creature : IConcept;

[GameConcept]
public record struct Player : IConcept;

[GameConcept]
public record struct Protagonist : IConcept;

[GameAspect]
public record struct Health { public int Initial; public int Max; }
public record struct CombatStats { public int BaseDamage; public int BaseDefense; }
```

### 3. Load, Build & Access

```csharp
// Simple fluent API
World world = new Pipeline()
    .AddSource("Base", new JsonLoader(), "Data/")
    .AddSource("Mods", new JsonLoader(), "Mods/")
    .Build(out var diagnostics);

// Access - typed, zero string, intellisense
int hp  = world.FromConcept<Creature>().At<Hero>().Take<Health>().Initial;
int atk = world.FromConcept<Player>().At<Hero>().Take<CombatStats>().BaseDamage;

foreach (ref readonly var entry in world.FromConcept<Creature>().All) {
    Use(entry.Take<Health>());
}
```

`Hero` is a generated entry marker type implementing `IBelongTo<Creature>`, `IBelongTo<Player>`, `IBelongTo<Protagonist>` - compile-time safe.

---

## 📦 Packages

```bash
dotnet add package DataCatalyst                               # SourceGen + Core
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

### World - typed runtime catalog

Pipeline final result

```csharp
world.FromConcept<Creature>().At<Hero>().Take<Health>().Initial;
world.FromConcept<Creature>().TryGet<Element>(out var e)

// Concept-scoped view
var creatures = world.FromConcept<Creature>();
creatures.At<Hero>().Take<Health>().Initial;
```

### Concept

A concept, as its name implies, stands for a concept about something in your game.

```csharp
[GameConcept]
public record struct Creature : IConcept;
```

### Aspect

An aspect is a data unit attached to entries of a concept. Multiple concepts entries can share aspect types.

```csharp
[GameAspect]
public record struct Health { public int Initial; public int Max; }
```

### Loader

Implement `IDataLoader` to use any format (CSV, YAML, MsgPack, ...).

```csharp
public class CsvDataLoader : IDataLoader {
    public LoadResult LoadDirectory(string path) {
        foreach (var file in Directory.GetFiles(path, "*.csv"))
            // Parse CSV -> RawEntry with typed components
    }
    public LoadResult LoadFile(string path) => LoadDirectory(Path.GetDirectoryName(path));
}
```

### Pipeline Stage

Stage is your specialized processing step that hooks into the pipeline

```csharp
public class ValidateStage : IPipelineStage {
    public string Id => "ValidateCreatures";
    public void Execute(PipelineContext ctx) {
        foreach (var entry in ctx.Entries) {
            if (entry.Concepts.Contains("Creature") && !entry.HasAspect<Health>())
                ctx.Diagnostics.Warn($"{entry.Key} missing Health");
        }
    }
}
```

Inject into pipeline at five hooks: `StagePosition.AfterLoad`, `AfterMerge`, `AfterResolve`, `BeforeBuild`.

### Materializer

Bridge from DataCatalyst's World to engine-specific objects. Define a pattern once, SourceGen dispatches all aspects.

```csharp
[Materializer]
partial class EcsMaterializer : IMaterializer<Entity> {
    readonly World _w;
    void Apply<T>(Entity e, T c) where T : struct => _w.Add(e, c);
}

// Usage - ECS
var mat = new EcsMaterializer(world);
mat.Apply(entity, world.FromConcept<Creature>().At<Hero>());

// Usage - Godot/Unity with [Materialize]
[Materialize]
partial class Player : CharacterBody2D {
    public override void _Ready() => this.Materialize();
}
```

### Extensions

| Namespace                                 | Types                                                                            |
| ----------------------------------------- | -------------------------------------------------------------------------------- |
| `DataCatalyst.Extensions.Compare`         | `CompareOp`, `OperatorParser`                                                    |
| `DataCatalyst.Extensions.Composition`     | `TransitionDef`, `ConditionGroupDef`, `SensorConditionDef`, `SensorInfluenceDef` |
| `DataCatalyst.Extensions.Materialization` | `IMaterializer<T>`, `MaterializerAttribute`, `MaterializeAttribute`              |

---

## 🔌 Bundled Plugin

### StateEngine

Data-driven hierarchical FSM. States, signals, and transitions are data - behavior is never hardcoded.

```json
{
	"goblinAI": {
		"concept": "LocomotionStates",
		"stateGroup": {
			"groupId": "GoblinAI",
			"defaultState": "Patrol",
			"states": {
				"patrol": {
					"transitions": [
						{
							"targetState": "Chase",
							"priority": 100,
							"conditions": {
								"all": [
									{
										"signal": "PlayerDistance",
										"op": "<",
										"value": 8
									}
								]
							}
						}
					]
				}
			}
		}
	}
}
```

```csharp
// Bake - resolve string names to int IDs
var baked = StateEngineBaker.Bake(
    world.FromConcept<LocomotionStates>().At<GoblinAI>().Take<StateGroup>(),
    world
);

// Evaluate - ONE engine for ALL entities
var result = StateEngineEvaluator.Evaluate(
    baked.DefaultStateId, baked, viableStates,
    signalId => signalId switch {
        PlayerDistance => entity.DistanceToPlayer,
        _ => 0f
    });
```

StateEngine is originally designed for ECS: ONE system evaluates ALL entities, but is compatible with normal use-cases.

---

## ⚖️ License

Distributed under the MIT License. See [LICENSE](LICENSE).
