namespace DataCatalyst.Abstractions;

/// <summary>
/// Defines how data entries from different data sources are merged.
/// </summary>
public enum MergePolicy {
	/// <summary>
	/// Only add new entries. If an entry with the same key already exists, skip it.
	/// </summary>
	Additive,

	/// <summary>
	/// Merge at the component level. Overwrite components if they exist, otherwise add them.
	/// </summary>
	Patch,

	/// <summary>
	/// Merge at the field level. Overwrites fields within existing components if they differ from default values.
	/// </summary>
	FieldPatch,

	/// <summary>
	/// Replaces the entire entry if it already exists.
	/// </summary>
	Replace,

	/// <summary>
	/// Applied after inheritance and plugins, overlaying fields orthogonally (e.g., localization).
	/// </summary>
	Overlay
}
