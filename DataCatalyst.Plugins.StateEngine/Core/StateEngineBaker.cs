namespace DataCatalyst.StateEngine.Core;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst;
using DataCatalyst.Compare;
using DataCatalyst.Composition;
using DataCatalyst.Generated;
using DataCatalyst.Knowledge;
using DataCatalyst.Registry;
using DataCatalyst.StateEngine.Models;

public class StateEngineBaker : Pipeline.IBaker<StateGroup, BakedStateGroup> {
	/// <summary>
	/// Static helper for direct/backward-compatible baking.
	/// </summary>
	public static BakedStateGroup Bake(string groupBeingName, StateGroup group, Knowledge world) {
		var diagnostics = new DiagnosticBag();
		var result = new StateEngineBaker().Bake(groupBeingName, group, world, diagnostics);
		if (diagnostics.HasErrors) {
			throw new InvalidOperationException(string.Join("\n", diagnostics.Items));
		}
		return result;
	}

	/// <summary>
	/// IBaker interface implementation.
	/// </summary>
	public BakedStateGroup Bake(string beingKey, StateGroup source, Knowledge knowledge, DiagnosticBag diagnostics) {
		ArgumentNullException.ThrowIfNull(beingKey);
		ArgumentNullException.ThrowIfNull(knowledge);

		if (source.States == null || source.States.Count == 0) {
			return new BakedStateGroup {
				GroupId = beingKey,
				States = FrozenDictionary<Ref<State>, BakedState>.Empty
			};
		}

		// Collect all state types
		var stateTypes = new HashSet<Type>();
		var stateBeingTypes = new Dictionary<string, Type>();

		foreach (var stateName in source.States) {
			var stateType = FindBeingType(stateName);
			if (stateType == null) {
				diagnostics.Warn($"State being '{stateName}' in group '{beingKey}' not found in registry");
				continue;
			}
			stateBeingTypes[stateName] = stateType;
			stateTypes.Add(stateType);
		}

		var statesMap = new Dictionary<Ref<State>, BakedState>();
		var statePool = knowledge.GetPool(typeof(State));
		if (statePool == null) {
			diagnostics.Error("State concept pool not found in world");
			return new BakedStateGroup {
				GroupId = beingKey,
				States = FrozenDictionary<Ref<State>, BakedState>.Empty
			};
		}

		foreach (var stateName in source.States) {
			if (!stateBeingTypes.TryGetValue(stateName, out var stateType) || stateType == null) {
				continue;
			}

			var stateIdx = knowledge.GetBeingIndex(stateType);
			if (stateIdx < 0) {
				continue;
			}

			// Check if this being has StateTransitions aspect
			StateTransitions stateTransitions;
			try {
				stateTransitions = statePool.Get<StateTransitions>(stateIdx);
			}
			catch {
				stateTransitions = new StateTransitions { Transitions = [] };
			}

			var bakedTransitions = new List<BakedTransition>();

			if (stateTransitions.Transitions != null) {
				foreach (var t in stateTransitions.Transitions) {
					if (t.TargetState.BeingType == null) {
						diagnostics.Warn($"Transition in state '{stateName}' has null or unresolved TargetState");
						continue;
					}

					var targetType = t.TargetState.BeingType;
					if (!stateTypes.Contains(targetType)) {
						diagnostics.Warn($"Transition target state '{targetType.Name}' in state '{stateName}' is not defined in the StateGroup");
					}

					var basePriority = (source.PriorityTier * source.TierScale) + t.Priority;

					bakedTransitions.Add(new BakedTransition {
						TargetState = new Ref<State>(targetType),
						BasePriority = basePriority,
						Conditions = BakeConditions(t.Conditions, diagnostics),
						Influences = BakeInfluences(t.Influences, diagnostics),
					});
				}
			}

			bakedTransitions.Sort(static (a, b) => b.BasePriority.CompareTo(a.BasePriority));

			var refState = new Ref<State>(stateType);
			statesMap[refState] = new BakedState {
				State = refState,
				Transitions = [.. bakedTransitions],
			};
		}

		var bakedGroup = new BakedStateGroup {
			GroupId = beingKey,
			States = statesMap.ToFrozenDictionary()
		};

		if (!string.IsNullOrEmpty(source.DefaultState)) {
			var defType = FindBeingType(source.DefaultState);
			if (defType != null && stateTypes.Contains(defType)) {
				bakedGroup.DefaultState = new Ref<State>(defType);
			}
			else {
				diagnostics.Warn($"Default state '{source.DefaultState}' in group '{beingKey}' not found or not in group states list");
			}
		}

		return bakedGroup;
	}

	private static Type? FindBeingType(string key) {
		foreach (var r in BeingRegistry.All) {
			if (r.BeingType.Name.Equals(key, StringComparison.OrdinalIgnoreCase)) {
				return r.BeingType;
			}
		}
		return null;
	}

	private static BakedConditionGroup? BakeConditions(ConditionGroupDef? conditions, DiagnosticBag diagnostics) {
		if (conditions == null) {
			return null;
		}

		if (!conditions.HasValue) {
			return null;
		}

		var c = conditions.Value;
		return new BakedConditionGroup {
			All = c.All?.Select(cond => BakeCondition(cond, diagnostics)).Where(x => x != null).Select(x => x!).ToArray() ?? [],
			Any = c.Any?.Select(cond => BakeCondition(cond, diagnostics)).Where(x => x != null).Select(x => x!).ToArray() ?? [],
			None = c.None?.Select(cond => BakeCondition(cond, diagnostics)).Where(x => x != null).Select(x => x!).ToArray() ?? [],
		};
	}

	private static BakedSensorCondition? BakeCondition(SensorConditionDef c, DiagnosticBag diagnostics) {
		if (c.Sensor.BeingType == null) {
			diagnostics.Warn("Sensor condition has null or unresolved Sensor reference");
			return null;
		}

		return new BakedSensorCondition {
			Sensor = c.Sensor,
			Op = OperatorParser.Parse(c.Op),
			Value = c.Value,
			ExitValue = c.ExitValue,
		};
	}

	private static BakedSensorInfluence[] BakeInfluences(List<SensorInfluenceDef>? influences, DiagnosticBag diagnostics) {
		if (influences == null) {
			return [];
		}

		var result = new List<BakedSensorInfluence>();
		foreach (var i in influences) {
			if (i.Sensor.BeingType == null) {
				diagnostics.Warn("Sensor influence has null or unresolved Sensor reference");
				continue;
			}

			result.Add(new BakedSensorInfluence {
				Sensor = i.Sensor,
				Weight = i.Weight,
			});
		}

		return [.. result];
	}
}
