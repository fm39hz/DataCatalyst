using System;
using System.Collections.Generic;
using DataCatalyst.Pipeline;

namespace DataCatalyst.Loader;

public sealed class DataSource
{
    public string Name { get; }
    public IDataLoader Loader { get; }
    public string Path { get; }
    public int Priority { get; set; }
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();
    public MergePolicy MergePolicy { get; set; } = MergePolicy.Patch;
    public IReadOnlyList<string>? Scope { get; init; }

    public DataSource(string name, IDataLoader loader, string path)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Loader = loader ?? throw new ArgumentNullException(nameof(loader));
        Path = path ?? throw new ArgumentNullException(nameof(path));
    }
}
