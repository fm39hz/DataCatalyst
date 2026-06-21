namespace DataCatalyst.Plugins.GameConcept;

using System;

/// <summary>
/// Declares a concept. On a struct (Kind=null or "default"): entry grouping.
/// On an enum with a specific Kind: processed by the appropriate plugin's SourceGen.
/// Kind is an extensible string — plugins define their own constants (e.g. "state", "sensor").
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class DataConceptAttribute : Attribute {
	/// <summary>Concept name (e.g. "Weapon", "AIState").</summary>
	public string Name { get; }

	/// <summary>
	/// Concept kind. Null/"default" for struct concepts. Plugin-specific kinds (e.g. "state", "sensor") for enums.
	/// Plugins define their own kind constants. GameConcept processes Default kinds.
	/// </summary>
	public string? Kind { get; set; }

	/// <summary>Declares a concept with the given name.</summary>
	public DataConceptAttribute(string name) => Name = name;
}
