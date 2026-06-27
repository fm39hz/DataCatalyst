namespace DataCatalyst.StateEngine.Models;

using System;
using System.Collections.Frozen;
using DataCatalyst;
using DataCatalyst.Compare;
using DataCatalyst.Composition;

public sealed class BakedStateGroup {
	private string _groupId = string.Empty;
	public string GroupId {
		get => _groupId;
		init => _groupId = value ?? throw new ArgumentNullException(nameof(GroupId));
	}

	public Ref<State> DefaultState { get; init; }

	private FrozenDictionary<Ref<State>, BakedState> _states = FrozenDictionary<Ref<State>, BakedState>.Empty;
	public FrozenDictionary<Ref<State>, BakedState> States {
		get => _states;
		init => _states = value ?? FrozenDictionary<Ref<State>, BakedState>.Empty;
	}

	private string _requiredTrait = string.Empty;
	public string RequiredTrait {
		get => _requiredTrait;
		init => _requiredTrait = value ?? string.Empty;
	}

	public float PriorityTier { get; init; }
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
