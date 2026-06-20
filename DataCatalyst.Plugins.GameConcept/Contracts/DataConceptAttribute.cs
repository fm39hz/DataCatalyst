namespace DataCatalyst.Plugins.GameConcept;

using System;

/// <summary>
/// Declares a concept. On a struct with Kind=Default (or no Kind): entry grouping.
/// On an enum with Kind=State: state machine enum. On an enum with Kind=Sensor: sensor enum.
/// SourceGen auto-generates registrations or mappers depending on target type and Kind.
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class DataConceptAttribute : Attribute {
	/// <summary>Concept name (e.g. "Weapon", "AIState").</summary>
	public string Name { get; }

	/// <summary>Kind of concept. Default (struct), State (enum), Sensor (enum).</summary>
	public ConceptKind Kind { get; set; } = ConceptKind.Default;

	/// <summary>Declares a concept with the given name.</summary>
	public DataConceptAttribute(string name) => Name = name;
}
