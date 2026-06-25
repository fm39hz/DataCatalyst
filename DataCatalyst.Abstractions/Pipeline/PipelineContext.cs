using System;
using System.Collections.Generic;
using DataCatalyst.Loader;
using DataCatalyst.Storage;
using WorldType = DataCatalyst.World.World;

namespace DataCatalyst.Pipeline;

public sealed class PipelineContext
{
    public DiagnosticBag Diagnostics { get; } = new();
    internal WorldType? World { get; set; }
    public List<DataSource>? SortedSources { get; set; }
    public List<RawEntry> RawEntries { get; set; } = new();
    public HashSet<string> AllKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ResolvedEntry> Entries { get; } = new();
}
