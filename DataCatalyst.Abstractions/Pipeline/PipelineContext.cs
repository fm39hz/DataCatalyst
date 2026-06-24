using System.Collections.Generic;
using WorldType = DataCatalyst.World.World;

namespace DataCatalyst.Pipeline;

public sealed class PipelineContext
{
    public DiagnosticBag Diagnostics { get; } = new();
    internal List<object> RawEntries { get; set; } = new();
    internal Dictionary<System.Type, object> Pools { get; } = new();
    internal WorldType? World { get; set; }
    public Dictionary<string, object> Bag { get; } = new();
}
