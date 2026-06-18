namespace DataCatalyst.Abstractions;

using System;

/// <summary>Marks a type as a data component.</summary>
[AttributeUsage(
	AttributeTargets.Struct,
	AllowMultiple = false, Inherited = false)]
public sealed class DataComponentAttribute : Attribute {
}
