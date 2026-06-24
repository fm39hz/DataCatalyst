using System.Collections.Generic;
using DataCatalyst.Compare;

namespace DataCatalyst.StateEngine.Models;

public sealed class BakedStateGroup
{
    public string GroupId { get; init; } = string.Empty;
    public int DefaultStateId { get; init; }
    public IReadOnlyDictionary<int, BakedState> States => MutableStates;
    internal Dictionary<int, BakedState> MutableStates { get; } = new();
}

public sealed class BakedState
{
    public int StateId { get; init; }
    public BakedTransition[] Transitions { get; init; } = new BakedTransition[0];
}

public sealed class BakedTransition
{
    public int TargetStateId { get; init; }
    public float BasePriority { get; init; }
    public BakedConditionGroup? Conditions { get; init; }
    public BakedSensorInfluence[] Influences { get; init; } = new BakedSensorInfluence[0];
}

public sealed class BakedConditionGroup
{
    public BakedSensorCondition[] All { get; init; } = new BakedSensorCondition[0];
    public BakedSensorCondition[] Any { get; init; } = new BakedSensorCondition[0];
    public BakedSensorCondition[] None { get; init; } = new BakedSensorCondition[0];
}

public sealed class BakedSensorCondition
{
    public int SignalId { get; init; }
    public CompareOp Op { get; init; }
    public float Value { get; init; }
    public float? ExitValue { get; init; }
}

public sealed class BakedSensorInfluence
{
    public int SignalId { get; init; }
    public float Weight { get; init; }
}
