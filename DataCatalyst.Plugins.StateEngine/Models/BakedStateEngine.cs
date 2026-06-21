namespace DataCatalyst.Plugins.StateEngine.Models;

using System.Collections.Generic;

/// <summary>Baked, data-driven state machine with int state/sensor IDs.</summary>
public sealed class BakedStateGroup {

	/// <summary>Unique identifier of the state group.</summary>
	public string GroupId { get; init; } = string.Empty;

	/// <summary>Hash ID of the default state.</summary>
	public int DefaultStateId { get; init; }

	/// <summary>States mapped by hash ID.</summary>
	public IReadOnlyDictionary<int, BakedState> States => MutableStates;

	internal Dictionary<int, BakedState> MutableStates { get; } = [];
}

/// <summary>Baked representation of a single state and its pre-flattened transitions.</summary>
public sealed class BakedState {

	/// <summary>Hash ID of the state.</summary>
	public int StateId { get; init; }

	/// <summary>Flat array of all transitions from this state (and its parents).</summary>
	public BakedTransition[] Transitions { get; init; } = [];
}

/// <summary>Baked transition with resolved priorities and pre-parsed conditions.</summary>
public sealed class BakedTransition {

	/// <summary>Hash ID of the target state.</summary>
	public int TargetStateId { get; init; }

	/// <summary>Pre-calculated base priority.</summary>
	public float BasePriority { get; init; }

	/// <summary>Pre-parsed evaluation conditions.</summary>
	public BakedConditionGroup? Conditions { get; init; }

	/// <summary>Pre-resolved sensor influences affecting priority.</summary>
	public BakedSensorInfluence[] Influences { get; init; } = [];
}

/// <summary>Baked condition group containing pre-resolved sensor conditions.</summary>
public sealed class BakedConditionGroup {

	/// <summary>All conditions must pass (AND).</summary>
	public BakedSensorCondition[] All { get; init; } = [];

	/// <summary>At least one condition must pass (OR).</summary>
	public BakedSensorCondition[] Any { get; init; } = [];

	/// <summary>No conditions must pass (NOT).</summary>
	public BakedSensorCondition[] None { get; init; } = [];
}

/// <summary>Baked sensor condition with pre-parsed operator.</summary>
public sealed class BakedSensorCondition {

	/// <summary>Hash ID of the sensor signal.</summary>
	public int SignalId { get; init; }

	/// <summary>The pre-parsed comparison operator.</summary>
	public Extensions.Compare.CompareOp Op { get; init; }

	/// <summary>Threshold value for triggering.</summary>
	public float Value { get; init; }

	/// <summary>Threshold when already at target state (null = reuse entry threshold).</summary>
	public float? ExitValue { get; init; }
}

/// <summary>Baked sensor influence affecting priority.</summary>
public sealed class BakedSensorInfluence {

	/// <summary>Hash ID of the sensor signal.</summary>
	public int SignalId { get; init; }

	/// <summary>Weight multiplier applied to sensor value.</summary>
	public float Weight { get; init; }
}
