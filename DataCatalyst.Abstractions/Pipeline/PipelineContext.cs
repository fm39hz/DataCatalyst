using System.Collections.Generic;
using DataCatalyst.Loader;
using DataCatalyst.Storage;
using WorldType = DataCatalyst.World.World;

namespace DataCatalyst.Pipeline;

public sealed class PipelineContext
{
    public DiagnosticBag Diagnostics { get; } = new();
    internal WorldType? World { get; set; }

    // ResolveSourceStage → LoadStage
    public List<DataSource>? SortedSources { get; set; }

    // LoadStage → MergeStage → InheritanceStage (raw JSON data, before type resolution)
    public List<RawEntry> RawEntries { get; set; } = new();

    // All entry keys for $ref validation (populated by LoadStage)
    public HashSet<string> AllKeys { get; set; } = new();

    // ResolveCrossRefStage → BuildWorldStage (typed aspects deserialized)
    public List<ResolvedEntry> Entries { get; } = new();
}
