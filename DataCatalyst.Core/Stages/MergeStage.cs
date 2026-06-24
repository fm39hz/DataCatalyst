using System;
using System.Collections.Generic;
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

            foreach (var kv in entry._rawFields)
            {
                existing._rawFields[kv.Key] = MergeField(existing._rawFields.GetValueOrDefault(kv.Key), kv.Value);
                if (!existing._fieldNames.Contains(kv.Key))
                    existing._fieldNames.Add(kv.Key);
            }

            if (entry.Inherits != null)
                existing.Inherits = entry.Inherits;

            ctx.Diagnostics.Info($"Merged source into '{entry.Key}'");
        }

        // Reassign indices
        var finalList = new List<RawEntry>(merged.Values);
        for (int i = 0; i < finalList.Count; i++)
            finalList[i].AssignedIndex = i;

        ctx.Bag["RawEntries"] = finalList;
    }

    private static object? MergeField(object? existing, object? incoming)
    {
        if (existing == null || incoming == null)
            return incoming ?? existing;

        // Both are dictionaries → recursive deep merge
        if (existing is Dictionary<string, object?> existDict &&
            incoming is Dictionary<string, object?> incDict)
        {
            foreach (var kv in incDict)
            {
                if (existDict.TryGetValue(kv.Key, out var subVal))
                    existDict[kv.Key] = MergeField(subVal, kv.Value);
                else
                    existDict[kv.Key] = kv.Value;
            }
            return existDict;
        }

        // Both are lists → append distinct
        if (existing is List<object?> existList &&
            incoming is List<object?> incList)
        {
            existList.AddRange(incList);
            return existList;
        }

        // Primitives → incoming wins (shallow field replace)
        return incoming;
    }
}
