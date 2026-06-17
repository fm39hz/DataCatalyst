# DataCatalyst.Plugins.StateMachine

Generic data-driven state machine plugin. Pure infra — `[DataComponent]` data model + evaluator. No baking step.

## Pipeline

```
DC pipeline → catalog.Get<StateGroup>("Locomotion")
  → StateMachineEvaluator.Evaluate(current, group, viableStates, readSensor)
  → Result.TargetStateId or nothing
```

## Types

```
Contracts/IStateMachineEvaluator     — evaluation contract
Models/StateGroup                    — GroupId, PriorityTier, TierScale, DepthPenalty, States
Models/StateDefinition               — Parent, Transitions
Core/StateMachineEvaluator           — pure eval function
```

## Evaluator

```csharp
StateMachineEvaluator.Evaluate(
    "Locomotion.Idle",                              // current state
    catalog.Get<StateGroup>("Locomotion"),          // from DC pipeline
    viableStates,                                   // HashSet<string>
    signalName => ReadSensor(entity, signalName))   // Func<string, float>
// → Result { TargetStateId = "Locomotion.Walk", HasValue = true }
```

Priority formula: `PriorityTier * TierScale + BasePriority - Depth * DepthPenalty` — all from data model, not hardcoded.
