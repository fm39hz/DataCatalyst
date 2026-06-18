namespace DataCatalyst.Plugins.StateEngine.Core;

using System;
using System.Collections.Generic;
using DataCatalyst.Plugins.NumericCompare.Core;
using Models;

/// <summary>Evaluates state transitions using pre-baked flat transition tables.</summary>
public static class StateEngineEvaluator<TState, TSensor> 
	where TState : notnull 
	where TSensor : notnull {

	/// <summary>Result of a state machine evaluation.</summary>
	public struct Result {
		/// <summary>Resolved target state identifier.</summary>
		public TState TargetStateId;

		/// <summary>Whether a valid transition was found.</summary>
		public bool HasValue;
	}

	/// <summary>
	/// Evaluates transitions on a baked group and returns the best target state, with zero allocations.
	/// The <paramref name="viableStates"/> set is read-only — reuse the same set across evaluations
	/// (via Clear + Add) to minimize allocation.
	/// </summary>
	public static Result Evaluate(
		TState currentStateId,
		BakedStateGroup<TState, TSensor> group,
		HashSet<TState> viableStates,
		Func<TSensor, float> readSensor) {

		if (group == null) {
			throw new ArgumentNullException(nameof(group));
		}
		if (viableStates == null) {
			throw new ArgumentNullException(nameof(viableStates));
		}
		if (readSensor == null) {
			throw new ArgumentNullException(nameof(readSensor));
		}

		if (!group.States.TryGetValue(currentStateId, out var currentState)) {
			return new Result { HasValue = false };
		}

			var bestTarget = default(TState);
			var bestPriority = float.MinValue;
			var hasBest = false;

			var transitions = currentState.Transitions;
			for (var i = 0; i < transitions.Length; i++) {
				var t = transitions[i];
				var target = t.TargetState;

				if (!viableStates.Contains(target)) {
					continue;
				}

				if (!PassConditions(t, currentStateId, target, readSensor)) {
					continue;
				}

				var priority = t.BasePriority;
				var influences = t.Influences;
				for (var j = 0; j < influences.Length; j++) {
					var inf = influences[j];
					priority += readSensor(inf.Signal) * inf.Weight;
				}

				if (priority > bestPriority) {
				bestPriority = priority;
				bestTarget = target;
				hasBest = true;
			}
		}

		return hasBest
			? new Result { TargetStateId = bestTarget!, HasValue = true }
			: new Result { HasValue = false };
	}

	private static bool PassConditions(
		BakedTransition<TState, TSensor> t,
		TState currentId,
		TState targetId,
		Func<TSensor, float> readSensor) {

		if (t.Conditions == null) {
			return true;
		}

		var conds = t.Conditions;
		var atTarget = EqualityComparer<TState>.Default.Equals(currentId, targetId);

		var all = conds.All;
		for (var i = 0; i < all.Length; i++) {
			if (!EvalSensor(all[i], readSensor, atTarget)) {
				return false;
			}
		}

		var any = conds.Any;
		if (any.Length > 0) {
			var anyOk = false;
			for (var i = 0; i < any.Length; i++) {
				if (EvalSensor(any[i], readSensor, atTarget)) {
					anyOk = true;
					break;
				}
			}

			if (!anyOk) {
				return false;
			}
		}

		var none = conds.None;
		for (var i = 0; i < none.Length; i++) {
			if (EvalSensor(none[i], readSensor, atTarget)) {
				return false;
			}
		}

		return true;
	}

	private static bool EvalSensor(
		BakedSensorCondition<TSensor> c,
		Func<TSensor, float> readSensor,
		bool atTarget) {

		var value = readSensor(c.Signal);
		var threshold = atTarget && c.ExitValue.HasValue ? c.ExitValue.Value : c.Value;
		return OperatorParser.Evaluate(value, c.Op, threshold);
	}
}
