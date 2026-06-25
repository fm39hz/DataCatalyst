namespace DataCatalyst.Pipeline;

using System;
using System.Collections.Generic;
using DataCatalyst.Loader;
using DataCatalyst.Storage;
using WorldType = World.World;

public sealed class PipelineContext {
	public DiagnosticBag Diagnostics { get; } = new();
	internal WorldType? World { get; set; }
	public List<DataSource>? SortedSources { get; set; }
	public List<RawEntry> RawEntries { get; set; } = [];
	public HashSet<string> AllKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, List<string>> Mappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public List<ResolvedEntry> Entries { get; } = [];
}
