namespace DataCatalyst.StateEngine.Models;

using System.Collections.Generic;
using DataCatalyst;
using DataCatalyst.Compare;
using DataCatalyst.Generated;

public sealed class BakedStateGroup {
	public string GroupId { get; init; } = string.Empty;
	public Ref<State> DefaultState { get; set; }
	public IReadOnlyDictionary<Ref<State>, BakedState> States => MutableStates;
	internal Dictionary<Ref<State>, BakedState> MutableStates { get; } = [];
}

public sealed class BakedState {
	public Ref<State> State { get; init; }
	public BakedTransition[] Transitions { get; init; } = [];
}

public sealed class BakedTransition {
	public Ref<State> TargetState { get; init; }
	public float BasePriority { get; init; }
	public BakedConditionGroup? Conditions { get; init; }
	public BakedSensorInfluence[] Influences { get; init; } = [];
}

public sealed class BakedConditionGroup {
	public BakedSensorCondition[] All { get; init; } = [];
	public BakedSensorCondition[] Any { get; init; } = [];
	public BakedSensorCondition[] None { get; init; } = [];
}

public sealed class BakedSensorCondition {
	public Ref<Sensor> Sensor { get; init; }
	public CompareOp Op { get; init; }
	public float Value { get; init; }
	public float? ExitValue { get; init; }
}

public sealed class BakedSensorInfluence {
	public Ref<Sensor> Sensor { get; init; }
	public float Weight { get; init; }
}
