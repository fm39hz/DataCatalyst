namespace DataCatalyst.Plugins.StateMachine.Contracts;

using System;
using System.Collections.Generic;
using DataCatalyst.Plugins.StateMachine.Models;

/// <summary>Evaluates state transitions based on sensor input.</summary>
public interface IStateMachineEvaluator {
	/// <summary>Result of a single evaluation pass.</summary>
	struct Result { public string TargetStateId; public bool HasValue; }

	/// <summary>Evaluates transitions and returns the best target.</summary>
	Result Evaluate(
		string currentStateId,
		StateGroup group,
		HashSet<string> viableStates,
		Func<string, float> readSensor);
}
