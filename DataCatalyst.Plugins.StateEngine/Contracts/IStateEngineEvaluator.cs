namespace DataCatalyst.Plugins.StateEngine.Contracts;

using System;
using System.Collections.Generic;
using Models;

/// <summary>Evaluates state transitions based on sensor input.</summary>
public interface IStateEngineEvaluator {
	/// <summary>Result of a single evaluation pass.</summary>
	public struct Result { public string TargetStateId; public bool HasValue; }

	/// <summary>Evaluates transitions and returns the best target.</summary>
	public Result Evaluate(
		string currentStateId,
		StateGroup group,
		HashSet<string> viableStates,
		Func<string, float> readSensor);
}
