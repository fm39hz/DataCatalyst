namespace DataCatalyst.Plugins.StateEngine.Core;

using System;
using System.Collections.Generic;
using DataCatalyst.Plugins.NumericCompare.Core;
using Models;
using Transition.Models;

/// <summary>Helper to bake and flatten hierarchical StateGroups into high-performance generic structures.</summary>
public static class StateEngineBaker {
	/// <summary>Bakes a string-based StateGroup into a flat, typesafe BakedStateGroup.</summary>
	public static BakedStateGroup<TState, TSensor> Bake<TState, TSensor>(
		StateGroup group,
		Func<string, TState> stateMapper,
		Func<string, TSensor> sensorMapper)
		where TState : notnull
		where TSensor : notnull {

		if (group == null) {
			throw new ArgumentNullException(nameof(group));
		}
		if (stateMapper == null) {
			throw new ArgumentNullException(nameof(stateMapper));
		}
		if (sensorMapper == null) {
			throw new ArgumentNullException(nameof(sensorMapper));
		}

		var bakedGroup = new BakedStateGroup<TState, TSensor> {
			GroupId = group.GroupId,
			DefaultState = !string.IsNullOrEmpty(group.DefaultState)
				? stateMapper(ResolveStateId(group.DefaultState, group.GroupId))
				: default!
		};

		foreach (var (stateKey, stateDef) in group.States) {
			var stateId = stateMapper(ResolveStateId(stateKey, group.GroupId));

			// Collect hierarchical states (from leaf to root)
			var chain = CollectHierarchy(stateKey, group.States);

			var bakedTransitionsList = new List<BakedTransition<TState, TSensor>>();

			for (var depth = 0; depth < chain.Count; depth++) {
				var srcDef = chain[depth];
				if (srcDef.Transitions == null) {
					continue;
				}

				foreach (var t in srcDef.Transitions) {
					var targetStr = ResolveStateId(t.TargetState, group.GroupId);
					var targetState = stateMapper(targetStr);

					var basePriority = (group.PriorityTier * group.TierScale) + t.Priority - (depth * group.DepthPenalty);

					var bakedConditions = BakeConditions(t.Conditions, sensorMapper);
					var bakedInfluences = BakeInfluences(t.Influences, sensorMapper);

					var bakedTransition = new BakedTransition<TState, TSensor> {
						TargetState = targetState,
						BasePriority = basePriority,
						Conditions = bakedConditions,
						Influences = bakedInfluences
					};

					bakedTransitionsList.Add(bakedTransition);
				}
			}

			var bakedState = new BakedState<TState, TSensor> {
				StateId = stateId,
				Transitions = [.. bakedTransitionsList]
			};

			bakedGroup.States[stateId] = bakedState;
		}

		return bakedGroup;
	}

	private static string ResolveStateId(string target, string familyId) =>
		target.Contains(".") ? target : $"{familyId}.{target}";

	private static List<StateDefinition> CollectHierarchy(
		string name,
		Dictionary<string, StateDefinition> allStates) {
		var result = new List<StateDefinition>();
		var visited = new HashSet<string>();
		var current = name;
		while (current != null && allStates.TryGetValue(current, out var def)) {
			if (!visited.Add(current)) {
				break; // Cycle check
			}

			result.Add(def);
			current = def.Parent;
		}

		return result;
	}

	private static BakedConditionGroup<TSensor>? BakeConditions<TSensor>(
		ConditionGroupDef? conds,
		Func<string, TSensor> sensorMapper)
		where TSensor : notnull {
		if (conds == null) {
			return null;
		}

		var all = BakeSensorConditions(conds.All, sensorMapper);
		var any = BakeSensorConditions(conds.Any, sensorMapper);
		var none = BakeSensorConditions(conds.None, sensorMapper);

		return new BakedConditionGroup<TSensor> {
			All = all,
			Any = any,
			None = none
		};
	}

	private static BakedSensorCondition<TSensor>[] BakeSensorConditions<TSensor>(
		List<SensorConditionDef>? conditions,
		Func<string, TSensor> sensorMapper)
		where TSensor : notnull {
		if (conditions == null) {
			return [];
		}

		var result = new BakedSensorCondition<TSensor>[conditions.Count];
		for (var i = 0; i < conditions.Count; i++) {
			var c = conditions[i];
			result[i] = new BakedSensorCondition<TSensor> {
				Signal = sensorMapper(c.Signal),
				Op = OperatorParser.Parse(c.Op),
				Value = c.Value,
				ExitValue = c.ExitValue
			};
		}

		return result;
	}

	private static BakedSensorInfluence<TSensor>[] BakeInfluences<TSensor>(
		List<SensorInfluenceDef>? influences,
		Func<string, TSensor> sensorMapper)
		where TSensor : notnull {
		if (influences == null) {
			return [];
		}

		var result = new BakedSensorInfluence<TSensor>[influences.Count];
		for (var i = 0; i < influences.Count; i++) {
			var inf = influences[i];
			result[i] = new BakedSensorInfluence<TSensor> {
				Signal = sensorMapper(inf.Signal),
				Weight = inf.Weight
			};
		}

		return result;
	}
}
