namespace DataCatalyst.StateEngine.Core;

using System;
using System.Collections.Generic;
using DataCatalyst;
using DataCatalyst.Generated;
using DataCatalyst.StateEngine.Models;

public static class StateEngineEvaluator {
	public readonly ref struct Result(Ref<State> targetState, bool hasValue) {
		public Ref<State> TargetState { get; } = targetState;
		public bool HasValue { get; } = hasValue;
	}

	public static Result Evaluate(
		Ref<State> currentState,
		BakedStateGroup group,
		IReadOnlyCollection<Ref<State>> viableStates,
		Func<Ref<Sensor>, float> readSensor) {
		ArgumentNullException.ThrowIfNull(group);
		ArgumentNullException.ThrowIfNull(viableStates);
		ArgumentNullException.ThrowIfNull(readSensor);

		if (!group.States.TryGetValue(currentState, out var currentBakedState)) {
			return new Result(default, false);
		}

		Ref<State> bestTarget = default;
		var bestPriority = float.MinValue;
		var transitions = currentBakedState.Transitions;

		for (var i = 0; i < transitions.Length; i++) {
			var t = transitions[i];
			if (!Contains(viableStates, t.TargetState)) {
				continue;
			}

			if (!PassConditions(t, currentState, t.TargetState, readSensor)) {
				continue;
			}

			var priority = t.BasePriority;
			var influences = t.Influences;
			for (var j = 0; j < influences.Length; j++) {
				priority += readSensor(influences[j].Sensor) * influences[j].Weight;
			}

			if (priority > bestPriority) {
				bestPriority = priority;
				bestTarget = t.TargetState;
			}
		}

		return bestTarget.BeingType != null
			? new Result(bestTarget, true)
			: new Result(default, false);
	}

	private static bool Contains(IReadOnlyCollection<Ref<State>> collection, Ref<State> item) {
		if (collection is HashSet<Ref<State>> hs) {
			return hs.Contains(item);
		}
		foreach (var x in collection) {
			if (x.Equals(item)) return true;
		}
		return false;
	}

	private static bool PassConditions(BakedTransition t, Ref<State> currentId, Ref<State> targetId,
		Func<Ref<Sensor>, float> readSensor) {
		if (t.Conditions == null) {
			return true;
		}

		var atTarget = currentId.Equals(targetId);
		var conds = t.Conditions;

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
				if (EvalSensor(any[i], readSensor, atTarget)) { anyOk = true; break; }
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

	private static bool EvalSensor(BakedSensorCondition c, Func<Ref<Sensor>, float> readSensor, bool atTarget) {
		var value = readSensor(c.Sensor);
		var threshold = atTarget && c.ExitValue.HasValue ? c.ExitValue.Value : c.Value;
		return Compare.OperatorParser.Evaluate(value, c.Op, threshold);
	}
}
