namespace DataCatalyst.Plugins.ConceptDomain;

using System;

/// <summary>
/// Marks a struct as a concept domain tag.
/// SourceGen auto-registers the concept name mapping.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class DataConceptAttribute : Attribute {
	/// <summary>The concept name (e.g., "Item", "Enemy").</summary>
	public string Name { get; }

	/// <summary>Declares a concept domain with the given name.</summary>
	public DataConceptAttribute(string name) => Name = name;
}
