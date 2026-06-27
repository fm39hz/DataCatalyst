namespace DataCatalyst.StateEngine.Core;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst;
using DataCatalyst.Compare;
using DataCatalyst.Composition;
using DataCatalyst.Knowledge;
using DataCatalyst.Registry;
using DataCatalyst.StateEngine.Models;
using DataCatalyst.Storage;

public class StateEngineBaker(IBeingRegistry registry) : Pipeline.IBaker<StateGroup, BakedStateGroup> {
	private readonly IBeingRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

	/// <summary>
	/// Static helper for direct/backward-compatible baking.
	/// </summary>
	public static BakedStateGroup Bake(string groupBeingName, StateGroup group, Knowledge world) {
		var diagnostics = new DiagnosticBag();
		var result = new StateEngineBaker(new BeingRegistry()).Bake(groupBeingName, group, world, diagnostics);
		if (diagnostics.HasErrors) {
			throw new InvalidOperationException(string.Join("\n", diagnostics.Items));
		}
		return result;
	}

	public BakedStateGroup Bake(string beingKey, StateGroup source, Knowledge knowledge, DiagnosticBag diagnostics) {
		ArgumentNullException.ThrowIfNull(beingKey);
		ArgumentNullException.ThrowIfNull(knowledge);

		if (source.States == null || source.States.Count == 0) {
			diagnostics.Warn($"State group '{beingKey}' has no states defined");
			return new BakedStateGroup {
				GroupId = beingKey,
				States = FrozenDictionary<Ref<State>, BakedState>.Empty
			};
		}

		var nameToType = BuildNameToTypeMap();
		var stateBeingTypes = BuildStateTypesMap(source.States, nameToType, diagnostics, out var stateTypes);

		var statePool = knowledge.GetPool(typeof(State));
		if (statePool == null) {
			diagnostics.Error("State concept pool not found in world");
			return new BakedStateGroup {
				GroupId = beingKey,
				States = FrozenDictionary<Ref<State>, BakedState>.Empty
			};
		}

		var statesMap = new Dictionary<Ref<State>, BakedState>();
		foreach (var stateName in source.States) {
			if (!stateBeingTypes.TryGetValue(stateName, out var stateType) || stateType == null) {
				continue;
			}

			BakeStateTransitions(stateName, stateType, statesMap, knowledge, source, statePool, stateTypes, diagnostics);
		}

		var defaultState = ComputeDefaultState(source.DefaultState, nameToType, stateTypes, beingKey, diagnostics);

		return new BakedStateGroup {
			GroupId = beingKey,
			States = statesMap.ToFrozenDictionary(),
			RequiredTrait = source.RequiredTrait,
			PriorityTier = source.PriorityTier,
			DefaultState = defaultState,
		};
	}

	private static Dictionary<string, Type> BuildStateTypesMap(
		List<string> states,
		Dictionary<string, Type> nameToType,
		DiagnosticBag diagnostics,
		out HashSet<Type> stateTypesSet)
	{
		stateTypesSet = new HashSet<Type>();
		var map = new Dictionary<string, Type>(states.Count, StringComparer.OrdinalIgnoreCase);
		foreach (var stateName in states) {
			var stateType = FindBeingType(stateName, nameToType, diagnostics);
			if (stateType == null) {
				continue;
			}

			map[stateName] = stateType;
			stateTypesSet.Add(stateType);
		}

		return map;
	}

	private static void BakeStateTransitions(
		string stateName,
		Type stateType,
		Dictionary<Ref<State>, BakedState> statesMap,
		Knowledge knowledge,
		StateGroup source,
		ITypedStoragePool statePool,
		HashSet<Type> stateTypes,
		DiagnosticBag diagnostics)
	{
		var stateIdx = knowledge.GetBeingIndex(stateType);
		if (stateIdx < 0) {
			return;
		}

		if (stateIdx >= statePool.Count) {
			diagnostics.Warn($"State index {stateIdx} out of range for '{stateName}'");
			return;
		}

		var stateTransitions = statePool.Get<StateTransitions>(stateIdx);

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

	private static Ref<State> ComputeDefaultState(
		string? defaultStateName,
		Dictionary<string, Type> nameToType,
		HashSet<Type> stateTypesSet,
		string beingKey,
		DiagnosticBag diagnostics)
	{
		var defStateType = !string.IsNullOrEmpty(defaultStateName)
			? FindBeingType(defaultStateName, nameToType, diagnostics)
			: null;

		if (defStateType != null && !stateTypesSet.Contains(defStateType)) {
			diagnostics.Warn($"Default state '{defaultStateName}' in group '{beingKey}' not in group states list");
			defStateType = null;
		}

		return defStateType != null ? new Ref<State>(defStateType) : default;
	}

	private Dictionary<string, Type> BuildNameToTypeMap() {
		var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
		foreach (var r in _registry.All) {
			map[r.BeingType.Name] = r.BeingType;
		}

		return map;
	}

	private static Type? FindBeingType(string key, Dictionary<string, Type> nameToType, DiagnosticBag diagnostics) {
		if (nameToType.TryGetValue(key, out var type)) {
			return type;
		}

		diagnostics.Warn($"State being '{key}' not found in registry");
		return null;
	}

	private static BakedConditionGroup? BakeConditions(ConditionGroupDef? conditions, DiagnosticBag diagnostics) {
		if (conditions is null) {
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

		CompareOp op;
		try { op = OperatorParser.Parse(c.Op); }
		catch (ArgumentException ex) {
			diagnostics.Warn($"Invalid operator '{c.Op}' in sensor condition: {ex.Message}");
			return null;
		}

		return new BakedSensorCondition {
			Sensor = c.Sensor,
			Op = op,
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

			if (!float.IsFinite(i.Weight)) {
				diagnostics.Warn($"Sensor influence has non-finite weight ({i.Weight}), skipping");
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
