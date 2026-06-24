using System;
using System.Collections.Generic;
using System.Linq;
using PipelineAbstractions = DataCatalyst.Pipeline;
using StageContext = DataCatalyst.Pipeline.PipelineContext;
using DataCatalyst.Storage;

namespace DataCatalyst.Stages;

internal sealed class ResolveCrossRefStage : PipelineAbstractions.IPipelineStage
{
    public string Id => "ResolveCrossRef";

    public void Execute(StageContext ctx)
    {
        var entries = ctx.Bag["RawEntries"] as List<RawEntry>;
        if (entries == null) return;

        var allKeys = ctx.Bag["AllKeys"] as HashSet<string>;
        if (allKeys == null) return;

        foreach (var entry in entries)
        {
            var resolvedCrossRefs = new Dictionary<string, object>();

            foreach (var kv in entry.CrossRefs)
            {
                if (allKeys.Contains(kv.Value))
                {
                    // Found target entry — store reference for BuildWorldStage
                    resolvedCrossRefs[kv.Key] = kv.Value;
                    ctx.Diagnostics.Info($"Resolved cross-ref '{entry.Key}.{kv.Key}' → '{kv.Value}'");
                }
                else
                {
                    ctx.Diagnostics.Warn($"Unresolved cross-ref '{entry.Key}.{kv.Key}' → '{kv.Value}'");
                }
            }
        }
    }
}
