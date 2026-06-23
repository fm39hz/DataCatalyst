namespace DataCatalyst.Plugins.StateEngine.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Core;
using DataCatalyst.Extensions.Compare;
using DataCatalyst.Extensions.Composition;
using Models;

/// <summary>Bakes string-based StateGroup data into a flat BakedStateGroup using int entry IDs from the catalog.</summary>
public static class StateEngineBaker {

	/// <summary>Bakes a StateGroup. Signal names are resolved to int entry IDs via the catalog.</summary>
	public static BakedStateGroup Bake(StateGroup group, DataCatalog catalog) {
		var stateNames = new HashSet<string>(group.States.Keys);
		var sensorNames = new HashSet<string>();

		foreach (var stateDef in group.States.Values) {
			if (!string.IsNullOrEmpty(stateDef.Parent)) stateNames.Add(stateDef.Parent);
			if (stateDef.Transitions != null) {
				foreach (var t in stateDef.Transitions) {
					stateNames.Add(t.TargetState);
					if (t.Conditions is { } conds) {
						if (conds.All != null) foreach (var c in conds.All) sensorNames.Add(c.Signal);
						if (conds.Any != null) foreach (var c in conds.Any) sensorNames.Add(c.Signal);
						if (conds.None != null) foreach (var c in conds.None) sensorNames.Add(c.Signal);
					}
				}
			}
		}

		var stateNameList = stateNames.Where(s => !string.IsNullOrEmpty(s)).OrderBy(s => s).ToList();

		var stateIdMap = new Dictionary<string, int>();
		for (int i = 0; i < stateNameList.Count; i++)
			stateIdMap[stateNameList[i]] = i + 1;

		var sensorIdMap = new Dictionary<string, int>();
		foreach (var name in sensorNames) {
			var id = catalog.GetEntryId(name);
			if (id >= 0) sensorIdMap[name] = id;
		}

		var defaultStateId = 0;
		if (!string.IsNullOrEmpty(group.DefaultState) && stateIdMap.TryGetValue(group.DefaultState, out var defId))
			defaultStateId = defId;

		var bakedGroup = new BakedStateGroup {
			GroupId = group.GroupId,
			DefaultStateId = defaultStateId,
		};

		foreach (var (stateKey, stateDef) in group.States) {
			if (!stateIdMap.TryGetValue(stateKey, out var sId)) continue;

			var chain = CollectHierarchy(stateKey, group.States);
			var bakedTransitionsList = new List<BakedTransition>();

			for (var depth = 0; depth < chain.Count; depth++) {
				var srcDef = chain[depth];
				if (srcDef.Transitions == null) continue;

				foreach (var t in srcDef.Transitions) {
					if (!stateIdMap.TryGetValue(t.TargetState, out var targetId)) continue;

					var basePriority = (float)(group.PriorityTier * group.TierScale) + t.Priority - (depth * group.DepthPenalty);

					bakedTransitionsList.Add(new BakedTransition {
						TargetStateId = targetId,
						BasePriority = basePriority,
						Conditions = BakeConditions(t.Conditions, sensorIdMap),
						Influences = BakeInfluences(t.Influences, sensorIdMap),
					});
				}
			}

			bakedTransitionsList.Sort(static (a, b) => b.BasePriority.CompareTo(a.BasePriority));

			bakedGroup.MutableStates[sId] = new BakedState {
				StateId = sId,
				Transitions = [.. bakedTransitionsList]
			};
		}

		return bakedGroup;
	}

	private static List<StateDefinition> CollectHierarchy(
		string name, Dictionary<string, StateDefinition> allStates) {
		var result = new List<StateDefinition>();
		var visited = new HashSet<string>();
		var current = (string?)name;
		while (current != null) {
			if (!allStates.TryGetValue(current, out var def))
				throw new KeyNotFoundException($"State hierarchy: '{current}' (ancestor of '{name}') not found.");
			if (!visited.Add(current))
				throw new InvalidOperationException($"Cycle detected in state hierarchy: '{current}' appears more than once.");
			result.Add(def);
			current = def.Parent;
		}
		return result;
	}

	private static BakedConditionGroup? BakeConditions(ConditionGroupDef? conds, Dictionary<string, int> sensorIdMap) {
		if (conds is null) return null;
		var c = conds.Value;
		return new BakedConditionGroup {
			All = BakeSensorConditions(c.All, sensorIdMap),
			Any = BakeSensorConditions(c.Any, sensorIdMap),
			None = BakeSensorConditions(c.None, sensorIdMap),
		};
	}

	private static BakedSensorCondition[] BakeSensorConditions(List<SensorConditionDef>? conditions, Dictionary<string, int> sensorIdMap) {
		if (conditions == null) return [];
		var result = new BakedSensorCondition[conditions.Count];
		for (var i = 0; i < conditions.Count; i++) {
			var c = conditions[i];
			sensorIdMap.TryGetValue(c.Signal, out var signalId);
			result[i] = new BakedSensorCondition {
				SignalId = signalId,
				Op = OperatorParser.Parse(c.Op),
				Value = c.Value,
				ExitValue = c.ExitValue,
			};
		}
		return result;
	}

	private static BakedSensorInfluence[] BakeInfluences(List<SensorInfluenceDef>? influences, Dictionary<string, int> sensorIdMap) {
		if (influences == null) return [];
		var result = new BakedSensorInfluence[influences.Count];
		for (var i = 0; i < influences.Count; i++) {
			var inf = influences[i];
			sensorIdMap.TryGetValue(inf.Signal, out var signalId);
			result[i] = new BakedSensorInfluence {
				SignalId = signalId,
				Weight = inf.Weight,
			};
		}
		return result;
	}
}
