namespace DataCatalyst.Plugins.StateEngine.Core;

using System;
using System.Collections.Generic;
using DataCatalyst.Extensions.Compare;
using Models;

/// <summary>Evaluates state transitions using pre-baked flat transition tables with int state IDs.</summary>
public static class StateEngineEvaluator {

	/// <summary>Result of a state machine evaluation.</summary>
	public readonly struct Result {
		/// <summary>Hash ID of the target state, or 0 if no transition found.</summary>
		public int TargetStateId { get; }

		/// <summary>Whether a valid transition was found.</summary>
		public bool HasValue { get; }

		public Result(int targetStateId, bool hasValue) {
			TargetStateId = targetStateId;
			HasValue = hasValue;
		}
	}

	/// <summary>Evaluates transitions and returns the best target state ID.</summary>
	public static Result Evaluate(
		int currentStateId,
		BakedStateGroup group,
		HashSet<int> viableStates,
		Func<int, float> readSensor) {

		if (group == null) throw new ArgumentNullException(nameof(group));
		if (viableStates == null) throw new ArgumentNullException(nameof(viableStates));
		if (readSensor == null) throw new ArgumentNullException(nameof(readSensor));

		if (!group.States.TryGetValue(currentStateId, out var currentState))
			return new Result(0, false);

		var bestTarget = 0;
		var bestPriority = float.MinValue;

		var transitions = currentState.Transitions;
		for (var i = 0; i < transitions.Length; i++) {
			var t = transitions[i];

			if (!viableStates.Contains(t.TargetStateId)) continue;
			if (!PassConditions(t, currentStateId, t.TargetStateId, readSensor)) continue;

			var priority = t.BasePriority;
			var influences = t.Influences;
			for (var j = 0; j < influences.Length; j++) {
				priority += readSensor(influences[j].SignalId) * influences[j].Weight;
			}

			if (priority > bestPriority) {
				bestPriority = priority;
				bestTarget = t.TargetStateId;
			}
		}

		return bestTarget != 0
			? new Result(bestTarget, true)
			: new Result(0, false);
	}

	private static bool PassConditions(
		BakedTransition t,
		int currentId,
		int targetId,
		Func<int, float> readSensor) {

		if (t.Conditions == null) return true;
		var atTarget = currentId == targetId;
		var conds = t.Conditions;

		var all = conds.All;
		for (var i = 0; i < all.Length; i++)
			if (!EvalSensor(all[i], readSensor, atTarget)) return false;

		var any = conds.Any;
		if (any.Length > 0) {
			var anyOk = false;
			for (var i = 0; i < any.Length; i++)
				if (EvalSensor(any[i], readSensor, atTarget)) { anyOk = true; break; }
			if (!anyOk) return false;
		}

		var none = conds.None;
		for (var i = 0; i < none.Length; i++)
			if (EvalSensor(none[i], readSensor, atTarget)) return false;

		return true;
	}

	private static bool EvalSensor(BakedSensorCondition c, Func<int, float> readSensor, bool atTarget) {
		var value = readSensor(c.SignalId);
		var threshold = atTarget && c.ExitValue.HasValue ? c.ExitValue.Value : c.Value;
		return OperatorParser.Evaluate(value, c.Op, threshold);
	}
}
