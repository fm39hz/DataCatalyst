namespace DataCatalyst.Abstractions;

using System;

/// <summary>
/// Assembly-level config for DataCatalyst SourceGen.
/// Controls generated namespace, usings, and injected attributes.
/// Type-safe — attributes passed as System.Type, not strings.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class DataCatalystConfigAttribute : Attribute {
	/// <summary>Namespace for generated components (default: DataCatalyst.Generated).</summary>
	public string Namespace { get; set; } = "DataCatalyst.Generated";

	/// <summary>Additional attributes to inject on ALL generated components.</summary>
	public Type[]? Attributes { get; set; }
}
