namespace DataCatalyst.Plugins.GameConcept;

using System;

/// <summary>
/// Declares a concept kind. On a struct: entry grouping.
/// On an enum with a Kind marker type: processed by plugin SourceGens.
/// Kind is a marker type (not a value) — each plugin defines its own marker structs
/// and their SourceGen recognizes them at compile time.
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class DataConceptAttribute : Attribute {
	/// <summary>Concept name (e.g. "Weapon", "AIState").</summary>
	public string Name { get; }

	/// <summary>
	/// Marker type identifying the concept kind (e.g. typeof(StateKind), typeof(SensorKind)).
	/// Null for struct concepts. Each plugin's SourceGen recognizes its own marker types.
	/// </summary>
	public Type? Kind { get; set; }

	/// <summary>Declares a concept with the given name.</summary>
	public DataConceptAttribute(string name) => Name = name;
}
