namespace DataCatalyst.Abstractions;

using System;

/// <summary>Marks a type as a data component.</summary>
[AttributeUsage(
	AttributeTargets.Struct | AttributeTargets.Class |
	AttributeTargets.Enum | AttributeTargets.Method,
	AllowMultiple = false, Inherited = false)]
public sealed class DataComponentAttribute : Attribute {
}
