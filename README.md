# DataCatalyst

[![NuGet Version](https://img.shields.io/nuget/v/DataCatalyst?style=flat-square)](https://www.nuget.org/packages/DataCatalyst/)
[![CI Status](https://img.shields.io/github/actions/workflow/status/fm39hz/DataCatalyst/ci.yml?branch=master&style=flat-square)](https://github.com/fm39hz/DataCatalyst/actions)
[![License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](LICENSE)

**DataCatalyst** is a foundational, compile-time composition framework for C# and .NET. It enforces a strict separation of concerns: **Code defines mechanism, Data defines content.**

---

## 💡 Philosophy & Core Vision

> **Code itself has no content.** Game logic, behaviors, and content values should never be hardcoded.

DataCatalyst is:

- 🚫 **NOT** a serializer.
- 🚫 **NOT** a behavior engine.
- 🚫 **NOT** a Domain-Specific Language (DSL).

It is a **pure infrastructure layer** that is completely mechanics-agnostic. How the resolved composition is utilized is entirely up to the consumer (e.g., your game engine or ECS runtime).

### 🔌 Modding & Patch Composition

DataCatalyst is designed from the ground up for modularity and mod compatibility. It behaves similarly to **SMAPI's ContentPatcher**:

- **Base Content**: Base game data is defined as a series of JSON files representing entities, items, or states.
- **Mod Patches**: Mods can supply their own JSON files. If a mod file declares an entry with the same key (e.g., `Entities.Goblin`), DataCatalyst automatically **merges** the components.
- **Component-Level Overrides**: Mod components overwrite existing base components or append new ones deterministically based on the loading order.
- **No C# Recompilation**: Designers and modders can alter composition, override values, and inject data without changing a single line of C# code.

---

## 🏗️ Project Architecture

DataCatalyst is divided into small, focused modules to keep the core generic and lightweight:

```
├── DataCatalyst.Abstractions   - [DataComponent], [DataPlugin], DataKey<T>, and contracts
├── DataCatalyst.Core           - Registry stores, Graph and Catalog builders, composition engine
├── DataCatalyst.SourceGen      - Roslyn Incremental Generator for static AOT/Trim registration
├── DataCatalyst.Loaders        - AOT-safe JSON file loader
└── DataCatalyst.Plugins.*      - Composable infrastructure plugins
    ├── NumericCompare          - Operator parser and threshold evaluation contract
    ├── Transition              - Transition and sensor condition data models
    └── StateMachine            - Generic data-driven state machine evaluator
```

### 🔁 Data Flow Pipeline

```mermaid
graph TD
    A[Raw JSON Files] -->|JsonDataLoader.LoadDirectory| B[DataEntry list]
    B -->|DataGraphBuilder.Build| C[DataGraph unresolved]
    C -->|DataCatalogBuilder.Resolve| D[DataCatalog resolved, immutable]

    subgraph Core Resolution
        D --> D1[1. Topological Plugin Init]
        D --> D2[2. Mod Patch / Duplicate Key Merge]
        D --> D3[3. Deep Inheritance DAG Flatten]
        D --> D4[4. Cycle Detection & Verification]
    end
```

---

## 🚀 Getting Started

### 1. Installation

Add the core packages to your projects. For central package versioning, add them to your `Directory.Build.props` or reference them directly:

```bash
dotnet add package DataCatalyst
dotnet add package DataCatalyst.Loaders
```

### 2. Define Components (C#)

Mark any plain struct as a component using the `[DataComponent]` attribute. The Source Generator will automatically discover it.

```csharp
using DataCatalyst.Abstractions;

namespace MyGame.Components;

[DataComponent]
public struct Health
{
    public float Current { get; init; }
    public float Max { get; init; }
}

[DataComponent]
public struct CombatStats
{
    public float AttackPower { get; init; }
    public float Defense { get; init; }
}
```

### 3. Compose Entries (JSON)

Compose your entities in JSON files. They can inherit from parents and override specific components.

`Data/BaseMonster.json`:

```json
{
	"Health": {
		"Current": 100,
		"Max": 100
	},
	"CombatStats": {
		"AttackPower": 10,
		"Defense": 5
	}
}
```

`Data/Goblin.json` (inherits from `BaseMonster` and overrides `Health` max, inheriting `CombatStats` automatically):

```json
{
	"inherits": ["BaseMonster"],
	"Health": {
		"Current": 50,
		"Max": 50
	}
}
```

### 4. Load and Resolve

Run the resolution pipeline at startup to build your resolved, immutable data catalog:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DataCatalyst.Core;
using DataCatalyst.Loaders;

// Configure JSON serialization options for Native AOT (e.g. using source-generated contexts)
var options = new JsonSerializerOptions
{
    TypeInfoResolver = new DefaultJsonTypeInfoResolver()
};

// 1. Load directory raw entries
var loadResult = JsonDataLoader.LoadDirectory("Data/", options);

// 2. Build the unresolved graph (automatically merges duplicate keys/mods)
var graph = DataGraphBuilder.Build(loadResult.Entries);

// 3. Resolve composition (processes inheritance, flattens hierarchies, and checks for cycles)
var catalog = DataCatalogBuilder.Resolve(graph);

// 4. Access resolved entries
if (catalog.TryGetEntry("Goblin", out var goblin))
{
    var health = goblin.Get<Health>();
    Console.WriteLine($"Goblin Health: {health.Current}/{health.Max}"); // Outputs: 50/50

    var stats = goblin.Get<CombatStats>();
    Console.WriteLine($"Goblin Attack: {stats.AttackPower}");          // Outputs: 10 (inherited!)
}
```

---

## ⚡ Native AOT & Trim Safety

DataCatalyst is engineered for **Native AOT (Ahead-of-Time) compilation** and strict trimming (essential for modern high-performance game runtimes such as Godot 4 .NET, Unity, or custom engines):

- **Zero Runtime Reflection**: The Roslyn source generator (`DataCatalyst.SourceGen`) scans your assemblies at compile time and registers component types inside static `[ModuleInitializer]` methods.
- **Explicit Type Resolution**: No runtime JSON type-name scanning. Components are resolved against statically registered types.
- **Trim-Safe Serialization**: `JsonDataLoader` accepts `JsonSerializerOptions` to integrate seamlessly with `System.Text.Json` source-generated serialization contexts.

---

## 📦 Bundled Infrastructure Plugins

DataCatalyst includes pure data-driven plugins for common game patterns:

### 🎮 StateMachine & Transition Plugins

Provides a generic, priority-based hierarchical state machine evaluator. It calculates transitions based on sensor inputs completely from data:

- **Transitions**: Priority-based edges from a source state to a target state.
- **Conditions**: Supports `All` (AND), `Any` (OR), and `None` (NOT) condition groups.
- **Hysteresis**: Supports separate entry `Value` and `ExitValue` parameters on conditions to prevent rapid flickering between states.
- **Hierarchical States**: State definitions can specify a `Parent`. If child transition conditions are not met, the evaluator automatically falls back to parent transitions.
- **Depth Penalty**: A penalty applied to parent transition priorities so that more specific child transitions win when competing.
- **Dynamic Influences**: Priorities can be dynamically modified by multiplying sensor values by configured weights.

```csharp
// Evaluate state transitions based on an active entity's state group and sensor readings
var result = StateMachineEvaluator.Evaluate(
    currentStateId: "Locomotion.Idle",
    group: catalog.Get<StateGroup>("Locomotion"),
    viableStates: activeViableStates,
    readSensor: signal => entity.GetSensorValue(signal)
);

if (result.HasValue)
{
    entity.TransitionTo(result.TargetStateId);
}
```

---

## ⚖️ License

Distributed under the MIT License. See [LICENSE](LICENSE) for more information.
