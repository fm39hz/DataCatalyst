# DataCatalyst.Plugins.Transition

Composition models representing transitions and sensor conditions.

## 📦 Data Models

All types are decorated with the `[DataComponent]` attribute, ensuring they are discovered at compile time.

### `TransitionDef` record

Defines a state transition:

* `TargetState` (`string`): Target state identifier.
* `Priority` (`int`): Base evaluation priority.
* `Conditions` (`ConditionGroupDef?`): Logical gates that must pass for this transition to be viable.
* `Influences` (`List<SensorInfluenceDef>?`): Dynamic sensor modifiers that affect the calculated priority.

### `ConditionGroupDef` record

Boolean logic gates:

* `All` (`List<SensorConditionDef>?`): All conditions must pass (AND).
* `Any` (`List<SensorConditionDef>?`): At least one condition must pass (OR).
* `None` (`List<SensorConditionDef>?`): No conditions must pass (NOT).

### `SensorConditionDef` record

Compares sensor readings against thresholds:

* `Signal` (`string`): Sensor signal key.
* `Op` (`string`): Comparison operator (e.g., `">"`, `"=="`, `"lt"`).
* `Value` (`float`): Trigger entry threshold.
* `ExitValue` (`float`): Hysteresis exit threshold (used only when the state machine is already at the target state).

### `SensorInfluenceDef` record

Modifies transition priority based on dynamic sensor readings:

* `Signal` (`string`): Sensor signal key.
* `Weight` (`float`): Multiplier applied to the sensor value and added to the transition priority.
