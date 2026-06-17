namespace DataCatalyst.Plugins.StateEngine.Core;

using System;
using System.Collections.Generic;
using DataCatalyst.Plugins.NumericCompare.Core;
using DataCatalyst.Plugins.Transition.Models;
using Models;

/// <summary>Evaluates state transitions based on sensor input.</summary>
public static class StateEngineEvaluator {
	/// <summary>Result of a state machine evaluation.</summary>
	public struct Result {
		/// <summary>Resolved target state identifier.</summary>
		public string TargetStateId;

		/// <summary>Whether a valid transition was found.</summary>
		public bool HasValue;
	}

	/// <summary>Evaluates transitions and returns the best target state.</summary>
	public static Result Evaluate(
		string currentStateId,
		StateGroup group,
		HashSet<string> viableStates,
		Func<string, float> readSensor) {
		var stateKey = currentStateId;
		var prefix = group.GroupId + ".";
		if (stateKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
			stateKey = stateKey[prefix.Length..];
		}

		if (!group.States.TryGetValue(stateKey, out var currentState)) {
			return new Result { HasValue = false };
		}

		var chain = CollectHierarchy(stateKey, group.States);
		var bestTarget = (string?)null;
		var bestPriority = int.MinValue;

		for (var depth = 0; depth < chain.Count; depth++) {
			var srcDef = chain[depth];
			if (srcDef.Transitions == null) {
				continue;
			}

			foreach (var t in srcDef.Transitions) {
				var target = ResolveStateId(t.TargetState, group.GroupId);
				if (!viableStates.Contains(target)) {
					continue;
				}

				if (!PassConditions(t, currentStateId, target, group.GroupId, readSensor)) {
					continue;
				}

				var priority = (group.PriorityTier * group.TierScale) + t.Priority - (depth * group.DepthPenalty);
				if (t.Influences != null) {
					foreach (var inf in t.Influences) {
						priority += (int)(readSensor(inf.Signal) * inf.Weight);
					}
				}

				if (priority > bestPriority) {
					bestPriority = priority;
					bestTarget = target;
				}
			}
		}

		return bestTarget != null
			? new Result { TargetStateId = bestTarget, HasValue = true }
			: new Result { HasValue = false };
	}

	private static bool PassConditions(TransitionDef t, string currentId, string targetId,
		string familyId, Func<string, float> readSensor) {
		var conds = t.Conditions;
		if (conds == null) {
			return false;
		}

		var currentResolved = ResolveStateId(currentId, familyId);
		var atTarget = currentResolved == targetId;

		if (conds.All != null) {
			foreach (var c in conds.All) {
				if (!EvalSensor(c, readSensor, atTarget)) {
					return false;
				}
			}
		}

		if (conds.Any != null) {
			var anyOk = false;
			foreach (var c in conds.Any) {
				if (EvalSensor(c, readSensor, atTarget)) {
					anyOk = true;
					break;
				}
			}

			if (!anyOk) {
				return false;
			}
		}

		if (conds.None != null) {
			foreach (var c in conds.None) {
				if (EvalSensor(c, readSensor, atTarget)) {
					return false;
				}
			}
		}

		return true;
	}

	private static bool EvalSensor(SensorConditionDef c, Func<string, float> readSensor, bool atTarget) {
		var value = readSensor(c.Signal);
		var threshold = atTarget && c.ExitValue != 0f ? c.ExitValue : c.Value;
		var op = OperatorParser.Parse(c.Op);
		return OperatorParser.Evaluate(value, op, threshold);
	}

	private static string ResolveStateId(string target, string familyId) =>
		target.Contains(".") ? target : $"{familyId}.{target}";

	private static List<StateDefinition> CollectHierarchy(string name,
		Dictionary<string, StateDefinition> allStates) {
		var result = new List<StateDefinition>();
		var visited = new HashSet<string>();
		var current = name;
		while (current != null && allStates.TryGetValue(current, out var def)) {
			if (!visited.Add(current)) {
				break;
			}

			result.Add(def);
			current = def.Parent;
		}

		return result;
	}
}
