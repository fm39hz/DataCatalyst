namespace DataCatalyst.StateEngine.Core;

using System;
using DataCatalyst;
using DataCatalyst.Generated;
using DataCatalyst.StateEngine.Models;

public interface ISensorReader {
	public float ReadSensor(Ref<Sensor> sensor);
}

public static class StateEngineEvaluator {
	public readonly struct Result(Ref<State> targetState, bool hasValue) {
		public Ref<State> TargetState { get; } = targetState;
		public bool HasValue { get; } = hasValue;
	}

	// High-Performance Struct-Reader overload
	public static Result Evaluate<TReader>(
		Ref<State> currentState,
		BakedStateGroup group,
		ReadOnlySpan<Ref<State>> viableStates,
		ref TReader reader) where TReader : struct, ISensorReader {
		ArgumentNullException.ThrowIfNull(group);

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

			if (!PassConditions(t, currentState, t.TargetState, ref reader)) {
				continue;
			}

			var priority = t.BasePriority;
			var influences = t.Influences;
			for (var j = 0; j < influences.Length; j++) {
				priority += reader.ReadSensor(influences[j].Sensor) * influences[j].Weight;
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

	// Delegate-based wrapper struct
	private readonly struct DelegateSensorReader(Func<Ref<Sensor>, float> readSensor) : ISensorReader {
		private readonly Func<Ref<Sensor>, float> _readSensor = readSensor;

		public readonly float ReadSensor(Ref<Sensor> sensor) => _readSensor(sensor);
	}

	// Delegate-based overload (wraps the delegate and delegates to the struct-based Evaluate)
	public static Result Evaluate(
		Ref<State> currentState,
		BakedStateGroup group,
		ReadOnlySpan<Ref<State>> viableStates,
		Func<Ref<Sensor>, float> readSensor) {
		ArgumentNullException.ThrowIfNull(readSensor);
		var reader = new DelegateSensorReader(readSensor);
		return Evaluate(currentState, group, viableStates, ref reader);
	}

	private static bool Contains(ReadOnlySpan<Ref<State>> span, Ref<State> item) {
		for (var i = 0; i < span.Length; i++) {
			if (span[i].Equals(item)) {
				return true;
			}
		}
		return false;
	}

	private static bool PassConditions<TReader>(BakedTransition t, Ref<State> currentId, Ref<State> targetId,
		ref TReader reader) where TReader : struct, ISensorReader {
		if (t.Conditions == null) {
			return true;
		}

		var atTarget = currentId.Equals(targetId);
		var conds = t.Conditions;

		var all = conds.All;
		for (var i = 0; i < all.Length; i++) {
			if (!EvalSensor(all[i], ref reader, atTarget)) {
				return false;
			}
		}

		var any = conds.Any;
		if (any.Length > 0) {
			var anyOk = false;
			for (var i = 0; i < any.Length; i++) {
				if (EvalSensor(any[i], ref reader, atTarget)) { anyOk = true; break; }
			}

			if (!anyOk) {
				return false;
			}
		}

		var none = conds.None;
		for (var i = 0; i < none.Length; i++) {
			if (EvalSensor(none[i], ref reader, atTarget)) {
				return false;
			}
		}

		return true;
	}

	private static bool EvalSensor<TReader>(BakedSensorCondition c, ref TReader reader, bool atTarget)
		where TReader : struct, ISensorReader {
		var value = reader.ReadSensor(c.Sensor);
		var threshold = atTarget && c.ExitValue.HasValue ? c.ExitValue.Value : c.Value;
		return Compare.OperatorParser.Evaluate(value, c.Op, threshold);
	}
}
