namespace DataCatalyst.Plugins.GameConcept;

using System;

/// <summary>
/// Assigns one or more concepts to a member of a [DataConcept] type.
/// Applies to enum values (state names, sensor names) or struct fields.
/// A single member can belong to multiple concepts (e.g. Attack→Combat, Flee→Combat+Motion).
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class ConceptMemberAttribute : Attribute {
	/// <summary>Concept names (e.g. "Combat", "Motion").</summary>
	public string[] Names { get; }

	/// <summary>Declares concept members.</summary>
	public ConceptMemberAttribute(params string[] names) => Names = names;
}
