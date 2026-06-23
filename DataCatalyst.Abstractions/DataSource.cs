namespace DataCatalyst.Abstractions;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents a structured, named data source containing configuration for path, priority,
/// dependencies, and merge strategy.
/// </summary>
public sealed class DataSource {
	/// <summary>
	/// Unique name of this data source, used for dependency sorting.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Format-agnostic loader used to parse files in this data source.
	/// </summary>
	public IDataLoader Loader { get; }

	/// <summary>
	/// Path to the directory or file representing this source.
	/// </summary>
	public string Path { get; }

	/// <summary>
	/// Priority score of this data source. Higher priority overrides lower priority.
	/// Used as tie-breaker for independent sources.
	/// </summary>
	public int Priority { get; init; } = 0;

	/// <summary>
	/// Names of other data sources that must be loaded before this one.
	/// </summary>
	public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();

	/// <summary>
	/// Merge policy applied when entries from this source collide with existing entries.
	/// </summary>
	public MergePolicy MergePolicy { get; init; } = MergePolicy.Patch;

	/// <summary>
	/// Optional list of concept names. If specified, this source only has authority
	/// over entries belonging to these concepts.
	/// </summary>
	public IReadOnlyList<string>? Scope { get; init; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DataSource"/> class.
	/// </summary>
	public DataSource(string name, IDataLoader loader, string path) {
#if NET6_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(name);
		ArgumentNullException.ThrowIfNull(loader);
		ArgumentNullException.ThrowIfNull(path);
#else
		if (name == null) throw new ArgumentNullException(nameof(name));
		if (loader == null) throw new ArgumentNullException(nameof(loader));
		if (path == null) throw new ArgumentNullException(nameof(path));
#endif
		Name = name;
		Loader = loader;
		Path = path;
	}
}
