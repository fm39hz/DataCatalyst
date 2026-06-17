# DataCatalyst.Plugins.StateMachine

A generic, data-driven hierarchical state machine plugin.

Provides pure infrastructure containing `[DataComponent]` models and a static evaluator with zero baking step.

## 🔄 Pipeline Flow

```
DataCatalog ──> catalog.Get<StateGroup>("GuardAI")
                 ↳ StateMachineEvaluator.Evaluate()
                     ↳ Result (TargetStateId or nothing)
```

## 📦 Data Models

### `StateGroup` record
Container for a logical group of states (e.g. `"GuardAI"`):
* `GroupId` (`string`): Unique group identifier.
* `PriorityTier` (`int`): Global tier priority.
* `TierScale` (`int`): Priority scaling multiplier (defaults to `10000`).
* `DepthPenalty` (`int`): Priority penalty per hierarchy depth level (defaults to `1000`).
* `DefaultState` (`string`): Fallback state.
* `States` (`Dictionary<string, StateDefinition>`): State definitions.

### `StateDefinition` record
Defines a single state and its parent for hierarchical inheritance:
* `Parent` (`string?`): Parent state key for inheriting transitions.
* `Transitions` (`List<TransitionDef>?`): Outgoing transitions.

---

## ⚡ Evaluator Engine

`StateMachineEvaluator.Evaluate` evaluates transitions and selects the best target state by finding the valid transition with the highest priority.

### Priority Formula

$$\text{Priority} = (\text{PriorityTier} \times \text{TierScale}) + \text{BasePriority} - (\text{Depth} \times \text{DepthPenalty}) + \sum (\text{SensorValue} \times \text{Weight})$$

* **BasePriority**: `t.Priority` defined on the transition.
* **Depth Penalty**: Encourages more specific child transitions to win over inherited parent transitions when they compete.
* **Dynamic Influences**: Adds sensor-based weight modifiers.

### Example Usage

```csharp
using DataCatalyst.Plugins.StateMachine.Core;

var result = StateMachineEvaluator.Evaluate(
    currentStateId: "GuardAI.Idle",
    group: catalog.Get<StateGroup>("GuardAI"),
    viableStates: new HashSet<string> { "GuardAI.Patrol", "GuardAI.Attack" },
    readSensor: signal => signal switch {
        "time" => 6.0f,
        "see_enemy" => 0.0f,
        _ => 0.0f
    }
);

if (result.HasValue)
{
    Console.WriteLine($"Transition to: {result.TargetStateId}");
}
```
