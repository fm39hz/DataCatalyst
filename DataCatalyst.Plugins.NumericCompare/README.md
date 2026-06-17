# DataCatalyst.Plugins.NumericCompare

A generic numeric comparison operator parser and evaluator. Used by transition and condition engines.

## 📦 API Reference

### `CompareOp` Enum
Supported comparison operators:
* `Equal` (maps to token `"=="` or `"eq"`)
* `NotEqual` (maps to token `"!="` or `"neq"`)
* `GreaterThan` (maps to token `">"` or `"gt"`)
* `GreaterThanOrEqual` (maps to token `">="` or `"gte"`)
* `LessThan` (maps to token `"<"` or `"lt"`)
* `LessThanOrEqual` (maps to token `"<="` or `"lte"`)

### `OperatorParser` Class
Static helpers for parsing and evaluating comparisons:
* `Parse(string token) → CompareOp`: Parses operator strings (case-insensitive).
* `Evaluate(float left, CompareOp op, float right) → bool`: Evaluates comparison using a default epsilon tolerance (`0.001f`) for floating-point equality.
