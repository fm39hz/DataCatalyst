namespace DataCatalyst.Extensions.Composition;

using System.Collections.Generic;
using DataCatalyst.Abstractions;

/// <summary>Defines a state transition with conditions.</summary>
[DataComponent]
public readonly record struct TransitionDef {
	/// <summary>Target state identifier.</summary>
	public string TargetState { get; init; }

	/// <summary>Transition evaluation priority.</summary>
	public int Priority { get; init; }

	/// <summary>Conditions that must pass for this transition.</summary>
	public ConditionGroupDef? Conditions { get; init; }

	/// <summary>Sensor influences that affect priority.</summary>
	public List<SensorInfluenceDef>? Influences { get; init; }
}

/// <summary>Group of sensor conditions with logical operators.</summary>
[DataComponent]
public readonly record struct ConditionGroupDef {
	/// <summary>All conditions must pass (AND).</summary>
	public List<SensorConditionDef>? All { get; init; }

	/// <summary>At least one condition must pass (OR).</summary>
	public List<SensorConditionDef>? Any { get; init; }

	/// <summary>No conditions must pass (NOT).</summary>
	public List<SensorConditionDef>? None { get; init; }
}

/// <summary>Sensor-based condition with operator and thresholds.</summary>
[DataComponent]
public readonly record struct SensorConditionDef {
	/// <summary>Sensor signal identifier.</summary>
	public string Signal { get; init; }

	/// <summary>Comparison operator string.</summary>
	public string Op { get; init; }

	/// <summary>Threshold value for triggering.</summary>
	public float Value { get; init; }

	/// <summary>Threshold when already at target state (null = reuse entry threshold).</summary>
	public float? ExitValue { get; init; }
}

/// <summary>Modifies transition priority based on sensor input.</summary>
[DataComponent]
public readonly record struct SensorInfluenceDef {
	/// <summary>Sensor signal identifier.</summary>
	public string Signal { get; init; }

	/// <summary>Weight multiplier applied to sensor value.</summary>
	public float Weight { get; init; }
}
