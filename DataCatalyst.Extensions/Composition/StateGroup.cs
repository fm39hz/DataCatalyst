namespace DataCatalyst.Composition;

using System.Collections.Generic;
using DataCatalyst.Attributes;

[GameAspect]
public readonly record struct StateGroup {
	public string GroupId { get; init; }
	public float PriorityTier { get; init; }
	public float TierScale { get; init; }
	public float DepthPenalty { get; init; }
	public string DefaultState { get; init; }
	public Dictionary<string, StateDefinition> States { get; init; }
}

[GameAspect]
public readonly record struct StateDefinition {
	public string? Parent { get; init; }
	public List<TransitionDef>? Transitions { get; init; }
}

[GameAspect]
public readonly record struct TransitionDef {
	public string TargetState { get; init; }
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
	public string Signal { get; init; }
	public string Op { get; init; }
	public float Value { get; init; }
	public float? ExitValue { get; init; }
}

[GameAspect]
public readonly record struct SensorInfluenceDef {
	public string Signal { get; init; }
	public float Weight { get; init; }
}
