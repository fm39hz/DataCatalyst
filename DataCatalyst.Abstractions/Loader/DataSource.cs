namespace DataCatalyst.Loader;

using System;
using System.Collections.Generic;
using DataCatalyst.Pipeline;

public sealed class DataSource(string name, IDataLoader loader, string path) {
	public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
	public IDataLoader Loader { get; } = loader ?? throw new ArgumentNullException(nameof(loader));
	public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
	public int Priority { get; set; }
	public IReadOnlyList<string> DependsOn { get; init; } = [];
	public MergePolicy MergePolicy { get; set; } = MergePolicy.Patch;
	public IReadOnlyList<string>? Scope { get; init; }
}
