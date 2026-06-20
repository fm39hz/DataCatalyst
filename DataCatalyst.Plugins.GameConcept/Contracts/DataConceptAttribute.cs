namespace DataCatalyst.Plugins.ConceptDomain;

using System;

/// <summary>
/// Marks a struct as a concept domain tag.
/// SourceGen auto-registers the concept name mapping.
/// </summary>
/// <remarks>Declares a concept domain with the given name.</remarks>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class DataConceptAttribute(string name) : Attribute {
	/// <summary>The concept name (e.g., "Item", "Enemy").</summary>
	public string Name { get; } = name;
}
