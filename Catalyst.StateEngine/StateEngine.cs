namespace Catalyst.StateEngine;

using System;
using Catalyst;
using Catalyst.Compare;
using Catalyst.Knowledge;
using Catalyst.Storage;

public static class StateEngine {
	public static Ref<State> Evaluate(
		ReadOnlySpan<BeingLinkDef> links,
		ReadOnlySpan<Ref<State>> viableStates,
		Ref<State> currentState,
		Func<Ref<Sensor>, float> readSensor,
		Func<Ref<State>, Desirability>? getDesirability = null)
	{
		Ref<State> bestTarget = default;
		var bestScore = float.MinValue;

		for (var i = 0; i < links.Length; i++) {
			var link = links[i];

			if (!Contains(viableStates, link.Target))
				continue;

			if (!PassGate(link, currentState, readSensor))
				continue;

			var score = 0f;
			if (getDesirability != null) {
				var des = getDesirability(link.Target);
				score = des.Priority;
				if (des.Influences != null) {
					for (var j = 0; j < des.Influences.Count; j++) {
						var inf = des.Influences[j];
						score += readSensor(inf.Sensor) * inf.Weight;
					}
				}
			}

			if (score > bestScore) {
				bestScore = score;
				bestTarget = link.Target;
			}
		}

		return bestTarget.IsValid ? bestTarget : currentState;
	}

	public static StateLinks GetLinks(Knowledge knowledge, Ref<State> state) {
		var pool = knowledge.GetPool(typeof(State));
		if (pool == null) return default;

		var idx = knowledge.GetBeingIndex(state.BeingType);
		if (idx < 0) return default;

		return pool.Get<StateLinks>(idx);
	}

	public static Desirability GetDesirability(Knowledge knowledge, Ref<State> state) {
		var pool = knowledge.GetPool(typeof(State));
		if (pool == null) return default;

		var idx = knowledge.GetBeingIndex(state.BeingType);
		if (idx < 0) return default;

		return pool.Get<Desirability>(idx);
	}

	private static bool Contains(ReadOnlySpan<Ref<State>> span, Ref<State> item) {
		for (var i = 0; i < span.Length; i++) {
			if (span[i].Equals(item)) return true;
		}
		return false;
	}

	private static bool PassGate(BeingLinkDef link, Ref<State> currentState,
		Func<Ref<Sensor>, float> readSensor)
	{
		if (link.Gate == null) return true;

		var atTarget = currentState.Equals(link.Target);
		var conds = link.Gate.Value;

		if (conds.All != null) {
			for (var i = 0; i < conds.All.Count; i++) {
				if (!EvalSensor(conds.All[i], readSensor, atTarget)) return false;
			}
		}

		if (conds.Any != null && conds.Any.Count > 0) {
			var anyOk = false;
			for (var i = 0; i < conds.Any.Count; i++) {
				if (EvalSensor(conds.Any[i], readSensor, atTarget)) { anyOk = true; break; }
			}
			if (!anyOk) return false;
		}

		if (conds.None != null) {
			for (var i = 0; i < conds.None.Count; i++) {
				if (EvalSensor(conds.None[i], readSensor, atTarget)) return false;
			}
		}

		return true;
	}

	private static bool EvalSensor(SensorConditionDef c, Func<Ref<Sensor>, float> readSensor, bool atTarget) {
		var value = readSensor(c.Sensor);
		var threshold = atTarget && c.ExitValue.HasValue ? c.ExitValue.Value : c.Value;
		return OperatorParser.Evaluate(value, OperatorParser.Parse(c.Op), threshold);
	}
}
