# DataCatalyst.Plugins.NumericCompare

Comparison operator enum and parser. Zero dependencies beyond Core.

## Types

```
Contracts/CompareOp           — Equal, NotEqual, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual
Core/OperatorParser           — Parse(string → CompareOp), Evaluate(value, op, threshold) → bool
```
