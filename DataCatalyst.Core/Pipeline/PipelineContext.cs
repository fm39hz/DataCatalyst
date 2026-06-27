namespace DataCatalyst.Pipeline;

using System;
using System.Collections.Generic;
using DataCatalyst.Knowledge;
using DataCatalyst.Loader;
using DataCatalyst.Schema;
using DataCatalyst.Storage;

public sealed class PipelineContext {
	public DiagnosticBag Diagnostics { get; } = new();
	public Knowledge? Knowledge { get; set; }
	public SchemaRegistry? Schema { get; set; }
	public List<DataSource>? SortedSources { get; set; }
	public List<RawBeing> RawBeings { get; set; } = [];
	public HashSet<string> AllKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, List<string>> Mappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public List<ResolvedBeing> Beings { get; } = [];
	public Dictionary<string, int> DynamicBeingIndices { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
