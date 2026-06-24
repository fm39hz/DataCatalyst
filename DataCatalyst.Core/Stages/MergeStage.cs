using System;
using System.Collections.Generic;
using System.Linq;
using PipelineAbstractions = DataCatalyst.Pipeline;
using StageContext = DataCatalyst.Pipeline.PipelineContext;
using DataCatalyst.Storage;

namespace DataCatalyst.Stages;

internal sealed class MergeStage : PipelineAbstractions.IPipelineStage
{
    public string Id => "Merge";

    public void Execute(StageContext ctx)
    {
        var entries = ctx.Bag["RawEntries"] as List<RawEntry>;
        if (entries == null || entries.Count == 0)
            return;

        // Group by key, apply priority-based merge
        var merged = new Dictionary<string, RawEntry>();

        foreach (var entry in entries)
        {
            if (!merged.TryGetValue(entry.Key, out var existing))
            {
                merged[entry.Key] = entry;
                continue;
            }

            // Simple priority merge: later source with same key wins
            // Full policy-based merge will be implemented later
            foreach (var kv in entry.Components)
                existing.Components[kv.Key] = kv.Value;

            foreach (var kv in entry.CrossRefs)
                existing.CrossRefs[kv.Key] = kv.Value;

            if (entry.Inherits != null)
                existing.Inherits = entry.Inherits;

            ctx.Diagnostics.Info($"Merged source into '{entry.Key}'");
        }

        // Reassign indices
        var finalList = merged.Values.ToList();
        for (int i = 0; i < finalList.Count; i++)
            finalList[i].AssignedIndex = i;

        ctx.Bag["RawEntries"] = finalList;
    }
}
