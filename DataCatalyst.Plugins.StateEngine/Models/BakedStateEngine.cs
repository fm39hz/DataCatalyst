namespace DataCatalyst.Plugins.StateEngine.Models;

using System.Collections.Generic;
using DataCatalyst.Extensions.Compare;

/// <summary>Baked, high-performance representation of a state group for generic types.</summary>
public sealed class BakedStateGroup<TState, TSensor>
	where TState : notnull
	where TSensor : notnull {

	/// <summary>Unique identifier of the state group.</summary>
	public string GroupId { get; init; } = string.Empty;

	/// <summary>The default target state when no transition is selected.</summary>
	public TState DefaultState { get; init; } = default!;

	private readonly Dictionary<TState, BakedState<TState, TSensor>> _states = [];

	/// <summary>Dictionary of states mapped by their generic type.</summary>
	public IReadOnlyDictionary<TState, BakedState<TState, TSensor>> States => _states;

	internal Dictionary<TState, BakedState<TState, TSensor>> MutableStates => _states;
}

/// <summary>Baked representation of a single state and its pre-flattened transitions.</summary>
public sealed class BakedState<TState, TSensor>
	where TState : notnull
	where TSensor : notnull {

	/// <summary>Identifier of the state.</summary>
	public TState StateId { get; init; } = default!;

	/// <summary>Flat array of all transitions from this state (and its parents) sorted/ordered.</summary>
	public BakedTransition<TState, TSensor>[] Transitions { get; init; } = [];
}

/// <summary>Baked transition definition with resolved priorities and typesafe keys.</summary>
public sealed class BakedTransition<TState, TSensor>
	where TState : notnull
	where TSensor : notnull {

	/// <summary>Target state identifier.</summary>
	public TState TargetState { get; init; } = default!;

	/// <summary>Pre-calculated base priority (incorporating parent hierarchy and depth penalty).</summary>
	public float BasePriority { get; init; }

	/// <summary>Pre-parsed evaluation conditions.</summary>
	public BakedConditionGroup<TSensor>? Conditions { get; init; }

	/// <summary>Pre-resolved sensor influences affecting priority.</summary>
	public BakedSensorInfluence<TSensor>[] Influences { get; init; } = [];
}

/// <summary>Baked condition group containing pre-resolved sensor conditions.</summary>
public sealed class BakedConditionGroup<TSensor>
	where TSensor : notnull {

	/// <summary>All conditions must pass (AND).</summary>
	public BakedSensorCondition<TSensor>[] All { get; init; } = [];

	/// <summary>At least one condition must pass (OR).</summary>
	public BakedSensorCondition<TSensor>[] Any { get; init; } = [];

	/// <summary>No conditions must pass (NOT).</summary>
	public BakedSensorCondition<TSensor>[] None { get; init; } = [];
}

/// <summary>Baked, typesafe representation of a sensor condition.</summary>
public sealed class BakedSensorCondition<TSensor>
	where TSensor : notnull {

	/// <summary>The resolved typesafe sensor identifier.</summary>
	public TSensor Signal { get; init; } = default!;

	/// <summary>The pre-parsed comparison operator.</summary>
	public CompareOp Op { get; init; }

	/// <summary>Threshold value for triggering.</summary>
	public float Value { get; init; }

	/// <summary>Threshold when already at target state (null = reuse entry threshold).</summary>
	public float? ExitValue { get; init; }
}

/// <summary>Baked, typesafe representation of a sensor influence.</summary>
public sealed class BakedSensorInfluence<TSensor>
	where TSensor : notnull {

	/// <summary>The resolved typesafe sensor identifier.</summary>
	public TSensor Signal { get; init; } = default!;

	/// <summary>Weight multiplier applied to sensor value.</summary>
	public float Weight { get; init; }
}
