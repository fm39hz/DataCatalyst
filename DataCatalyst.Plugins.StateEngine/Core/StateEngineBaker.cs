using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Composition;
using DataCatalyst.Compare;
using DataCatalyst.StateEngine.Models;
using WorldAbstractions = DataCatalyst.World;

namespace DataCatalyst.StateEngine.Core;

public static class StateEngineBaker
{
    public static BakedStateGroup Bake(StateGroup group, WorldAbstractions.World? world)
    {
        if (group.States == null || group.States.Count == 0)
            return new BakedStateGroup { GroupId = group.GroupId };

        // Collect all state names
        var stateNames = new HashSet<string>(group.States.Keys);
        foreach (var def in group.States.Values)
        {
            if (!string.IsNullOrEmpty(def.Parent)) stateNames.Add(def.Parent);
            if (def.Transitions != null)
            {
                foreach (var t in def.Transitions)
                {
                    stateNames.Add(t.TargetState);
                }
            }
        }

        var stateNameList = stateNames.Where(s => !string.IsNullOrEmpty(s))
            .OrderBy(s => s).ToList();

        var stateIdMap = new Dictionary<string, int>();
        for (int i = 0; i < stateNameList.Count; i++)
            stateIdMap[stateNameList[i]] = i + 1;

        var defaultStateId = 0;
        if (!string.IsNullOrEmpty(group.DefaultState) && stateIdMap.TryGetValue(group.DefaultState, out var defId))
            defaultStateId = defId;

        var bakedGroup = new BakedStateGroup
        {
            GroupId = group.GroupId,
            DefaultStateId = defaultStateId,
        };

        foreach (var (stateKey, stateDef) in group.States)
        {
            if (!stateIdMap.TryGetValue(stateKey, out var sId)) continue;

            var chain = CollectHierarchy(stateKey, group.States);
            var bakedTransitions = new List<BakedTransition>();

            for (var depth = 0; depth < chain.Count; depth++)
            {
                var srcDef = chain[depth];
                if (srcDef.Transitions == null) continue;

                foreach (var t in srcDef.Transitions)
                {
                    if (!stateIdMap.TryGetValue(t.TargetState, out var targetId)) continue;

                    var basePriority = (group.PriorityTier * group.TierScale)
                        + t.Priority - (depth * group.DepthPenalty);

                    bakedTransitions.Add(new BakedTransition
                    {
                        TargetStateId = targetId,
                        BasePriority = basePriority,
                        Conditions = BakeConditions(t.Conditions),
                        Influences = BakeInfluences(t.Influences),
                    });
                }
            }

            bakedTransitions.Sort(static (a, b) => b.BasePriority.CompareTo(a.BasePriority));

            bakedGroup.MutableStates[sId] = new BakedState
            {
                StateId = sId,
                Transitions = bakedTransitions.ToArray(),
            };
        }

        return bakedGroup;
    }

    private static List<StateDefinition> CollectHierarchy(string name,
        Dictionary<string, StateDefinition> allStates)
    {
        var result = new List<StateDefinition>();
        var visited = new HashSet<string>();
        var current = (string?)name;

        while (current != null && allStates.TryGetValue(current, out var def) && visited.Add(current))
        {
            result.Add(def);
            current = def.Parent;
        }

        result.Reverse();
        return result;
    }

    private static BakedConditionGroup? BakeConditions(ConditionGroupDef? conditions)
    {
        if (conditions == null) return null;

        if (!conditions.HasValue) return null;
        var c = conditions.Value;
        return new BakedConditionGroup
        {
            All = c.All?.Select(BakeCondition).ToArray() ?? System.Array.Empty<BakedSensorCondition>(),
            Any = c.Any?.Select(BakeCondition).ToArray() ?? System.Array.Empty<BakedSensorCondition>(),
            None = c.None?.Select(BakeCondition).ToArray() ?? System.Array.Empty<BakedSensorCondition>(),
        };
    }

    private static BakedSensorCondition BakeCondition(SensorConditionDef c)
    {
        return new BakedSensorCondition
        {
            SignalId = c.Signal.GetHashCode(),
            Op = OperatorParser.Parse(c.Op),
            Value = c.Value,
            ExitValue = c.ExitValue,
        };
    }

    private static BakedSensorInfluence[] BakeInfluences(List<SensorInfluenceDef>? influences)
    {
        if (influences == null) return System.Array.Empty<BakedSensorInfluence>();
        return influences.Select(i => new BakedSensorInfluence
        {
            SignalId = i.Signal.GetHashCode(),
            Weight = i.Weight,
        }).ToArray();
    }
}
