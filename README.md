# DataCatalyst

[![NuGet Version](https://img.shields.io/nuget/v/DataCatalyst?style=flat-square)](https://www.nuget.org/packages/DataCatalyst/)
[![CI Status](https://img.shields.io/github/actions/workflow/status/fm39hz/DataCatalyst/ci.yml?branch=master&style=flat-square)](https://github.com/fm39hz/DataCatalyst/actions)
[![License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](LICENSE)

**DataCatalyst** is an ontological concept composer framework for C#/.NET

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
	"Hero": {
		"$Creature": {
			"Health": { "Initial": 50, "Max": 50 },
			"CombatStats": { "BaseDamage": 8, "BaseDefense": 5 }
		},
		"$Player": {},
		"$Protagonist": {}
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

[GameAspect]
public record struct CombatStats { public int BaseDamage; public int BaseDefense; }
```

### 3. Load, Build & Access

```csharp
// Simple fluent API
Knowledge knowledge = new Pipeline()
    .AddSource("Base", new JsonDataLoader(), "Data/")
    .AddSource("Mods", new JsonDataLoader(), "Mods/")
    .Build(out var diagnostics);

// Access - typed
int hp  = knowledge.Of<Creature>().At<Hero>().Take<Health>().Initial;
int atk = knowledge.Of<Player>().At<Hero>().Take<CombatStats>().BaseDamage;
```

`Hero` is a generated `being` marker type implementing `IBelongTo<Creature>`, `IBelongTo<Player>`, `IBelongTo<Protagonist>` - compile-time safe.

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

### Knowledge - immutable catalog

Pipeline final result

```csharp
knowledge.Of<Creature>().At<Hero>().Take<Health>().Initial;

// Concept-scoped view
var creatures = knowledge.Of<Creature>();
creatures.At<Hero>().Take<Health>().Initial;
```

### Concept

A concept, as its name implies, stands for a concept about something in your game.

```csharp
[GameConcept]
public record struct Creature : IConcept;
```

### Aspect

An aspect is a data unit attached to beings of a concept. Multiple concepts beings can share aspect types.

```csharp
[GameAspect]
public record struct Health { public int Initial; public int Max; }
```

### Loader

Implement `IDataLoader` to use any format (CSV, YAML, MsgPack, ...).

```csharp
public class CsvDataLoader : IDataLoader {
    public LoadResult Load(string content, string fallbackKey) {
        var result = new LoadResult();
        // Parse CSV string content -> RawBeing
        return result;
    }

    public LoadResult LoadFile(string path) {
        return Load(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
    }

    public LoadResult LoadDirectory(string path) {
        var result = new LoadResult();
        foreach (var file in Directory.EnumerateFiles(path, "*.csv")) {
            var fileResult = LoadFile(file);
            // Combine fileResult beings, diagnostics, and mappings into result
        }
        return result;
    }
}
```

### Materializer

Bridge from DataCatalyst's Knowledge to engine-specific objects. Define a pattern once, SourceGen dispatches all aspects.

```csharp
[Materializer]
partial class EcsMaterializer : IMaterializer<Entity> {
    readonly Knowledge _k;
    void Apply<T>(Entity e, T c) where T : struct => _k.Add(e, c);
}

// Usage - ECS
var mat = new EcsMaterializer(knowledge);
mat.Apply(entity, knowledge.Of<Creature>().At<Hero>());

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
		"$LocomotionStates": {
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
}
```

```csharp
// Bake - resolve string names to int IDs
var baked = StateEngineBaker.Bake(
    knowledge.Of<LocomotionStates>().At<GoblinAI>().Take<StateGroup>(),
    knowledge
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

Distributed under the MIT License. See [LICENSE](LICENSE)
