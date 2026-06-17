namespace DataCatalyst.Plugins.NumericCompare.Core;

using System;
using System.Collections.Generic;
using Contracts;

/// <summary>Parses and evaluates numeric comparison operators.</summary>
public static class OperatorParser {
	private static readonly Dictionary<string, CompareOp> Map = new(StringComparer.OrdinalIgnoreCase) {
		["=="] = CompareOp.Equal,
		["eq"] = CompareOp.Equal,
		["!="] = CompareOp.NotEqual,
		["neq"] = CompareOp.NotEqual,
		[">"] = CompareOp.GreaterThan,
		["gt"] = CompareOp.GreaterThan,
		[">="] = CompareOp.GreaterThanOrEqual,
		["gte"] = CompareOp.GreaterThanOrEqual,
		["<"] = CompareOp.LessThan,
		["lt"] = CompareOp.LessThan,
		["<="] = CompareOp.LessThanOrEqual,
		["lte"] = CompareOp.LessThanOrEqual
	};

	/// <summary>Converts a string token to a CompareOp value.</summary>
	public static CompareOp Parse(string op) {
		if (Map.TryGetValue(op, out var result)) {
			return result;
		}

		throw new ArgumentException($"Unknown operator: {op}");
	}

	/// <summary>Evaluates a numeric comparison with tolerance.</summary>
	public static bool Evaluate(float value, CompareOp op, float threshold, float epsilon = 0.001f) => op switch {
		CompareOp.Equal => Math.Abs(value - threshold) < epsilon,
		CompareOp.NotEqual => Math.Abs(value - threshold) >= epsilon,
		CompareOp.GreaterThan => value > threshold,
		CompareOp.GreaterThanOrEqual => value >= threshold,
		CompareOp.LessThan => value < threshold,
		CompareOp.LessThanOrEqual => value <= threshold,
		_ => false
	};
}
