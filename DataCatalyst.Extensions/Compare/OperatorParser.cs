using System;

namespace DataCatalyst.Compare;

public static class OperatorParser
{
    public static CompareOp Parse(string op) => op switch
    {
        "==" or "=" => CompareOp.Equal,
        "!=" or "<>" => CompareOp.NotEqual,
        "<" => CompareOp.LessThan,
        "<=" => CompareOp.LessThanOrEqual,
        ">" => CompareOp.GreaterThan,
        ">=" => CompareOp.GreaterThanOrEqual,
        _ => throw new ArgumentException($"Unknown operator '{op}'", nameof(op)),
    };

    public static bool Evaluate(float value, CompareOp op, float threshold) => op switch
    {
        CompareOp.Equal => value == threshold,
        CompareOp.NotEqual => value != threshold,
        CompareOp.LessThan => value < threshold,
        CompareOp.LessThanOrEqual => value <= threshold,
        CompareOp.GreaterThan => value > threshold,
        CompareOp.GreaterThanOrEqual => value >= threshold,
        _ => false,
    };
}
