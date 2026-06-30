namespace DataCatalyst.StateEngine;

using System.Collections.Generic;
using DataCatalyst;
using DataCatalyst.Attributes;
using DataCatalyst.Compare;

[GameAspect]
public readonly record struct StateGroup : IRevealedBy<State> {
	public Ref<State> DefaultState { get; init; }
	public List<Ref<State>> States { get; init; }
	public float PriorityTier { get; init; }
	public float TierScale { get; init; }
	public string RequiredTrait { get; init; }
}

[GameAspect]
public readonly record struct StateLinks : IRevealedBy<State> {
	public List<BeingLinkDef>? Links { get; init; }
}

[GameAspect]
public readonly record struct BeingLinkDef {
	public Ref<State> Target { get; init; }
	public ConditionGroupDef? Gate { get; init; }
}

[GameAspect]
public readonly record struct Desirability : IRevealedBy<State> {
	public int Priority { get; init; }
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
	public Ref<Sensor> Sensor { get; init; }
	public string Op { get; init; }
	public float Value { get; init; }
	public float? ExitValue { get; init; }
}

[GameAspect]
public readonly record struct SensorInfluenceDef {
	public Ref<Sensor> Sensor { get; init; }
	public float Weight { get; init; }
}
