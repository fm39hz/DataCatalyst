namespace DataCatalyst.Extensions.Compare;

using System;
using System.Collections.Generic;

/// <summary>Parses and evaluates numeric comparison operators.</summary>
public static class OperatorParser {
	/// <summary>Default tolerance for floating-point comparison.</summary>
	public const float DefaultEpsilon = 0.001f;

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
#if NET6_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(op);
#else
		if (op == null) {
			throw new ArgumentNullException(nameof(op));
		}
#endif
		if (Map.TryGetValue(op, out var result)) {
			return result;
		}

		throw new ArgumentException($"Unknown operator: {op}");
	}

	/// <summary>Evaluates a numeric comparison with tolerance.</summary>
	public static bool Evaluate(float value, CompareOp op, float threshold, float epsilon = DefaultEpsilon) => op switch {
		CompareOp.Equal => Math.Abs(value - threshold) < epsilon,
		CompareOp.NotEqual => Math.Abs(value - threshold) >= epsilon,
		CompareOp.GreaterThan => value > threshold,
		CompareOp.GreaterThanOrEqual => value >= threshold,
		CompareOp.LessThan => value < threshold,
		CompareOp.LessThanOrEqual => value <= threshold,
		_ => false
	};
}
