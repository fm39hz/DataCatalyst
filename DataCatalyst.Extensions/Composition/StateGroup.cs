namespace DataCatalyst.Composition;

using System.Collections.Generic;
using DataCatalyst.Attributes;

[GameAspect]
public readonly record struct StateGroup {
	public string DefaultState { get; init; }
	public List<string> States { get; init; }
	public float PriorityTier { get; init; }
	public float TierScale { get; init; }
	public float DepthPenalty { get; init; }
}

[GameAspect]
public readonly record struct StateTransitions {
	public List<TransitionDef>? Transitions { get; init; }
}

[GameAspect]
public readonly record struct TransitionDef {
	public global::DataCatalyst.Ref<global::DataCatalyst.Generated.State> TargetState { get; init; }
	public int Priority { get; init; }
	public ConditionGroupDef? Conditions { get; init; }
	public List<SensorInfluenceDef>? Influences { get; init; }
}

[GameAspect]
public readonly record struct ConditionGroupDef {
	public List<SensorConditionDef>? All { get; init; }
	public List<SensorConditionDef>? Any { get; init; }
	public List<SensorConditionDef>? None { get; init; }
}

[GameAspect]
public readonly record struct SensorConditionDef {
	public global::DataCatalyst.Ref<global::DataCatalyst.Generated.Sensor> Sensor { get; init; }
	public string Op { get; init; }
	public float Value { get; init; }
	public float? ExitValue { get; init; }
}

[GameAspect]
public readonly record struct SensorInfluenceDef {
	public global::DataCatalyst.Ref<global::DataCatalyst.Generated.Sensor> Sensor { get; init; }
	public float Weight { get; init; }
}
