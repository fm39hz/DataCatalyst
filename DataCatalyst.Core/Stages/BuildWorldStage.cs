using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Pipeline;
using DataCatalyst.Registry;
using DataCatalyst.Storage;
using WorldAbstractions = DataCatalyst.World;

namespace DataCatalyst.Stages;

internal sealed class BuildWorldStage : IPipelineStage
{
    public string Id => "BuildWorld";

    public void Execute(PipelineContext ctx)
    {
        var entries = ctx.Entries;
        if (entries == null || entries.Count == 0)
        { ctx.Diagnostics.Error("No entries to build world"); return; }

        var byConcept = new Dictionary<string, List<ResolvedEntry>>();
        foreach (var e in entries)
            foreach (var c in e.Concepts)
                (byConcept.TryGetValue(c, out var l) ? l : (byConcept[c] = new())).Add(e);

        var pools = new Dictionary<Type, IStoragePool>();
        var entryIndices = new Dictionary<Type, int>();

        foreach (var kv in byConcept)
        {
            var ct = FindConceptType(kv.Key);
            if (ct == null)
            { ctx.Diagnostics.Warn($"Concept '{kv.Key}' has no registered type — skipping"); continue; }

            var ce = kv.Value;
            int maxIdx = ce.Max(e => e.AssignedIndex);
            if (maxIdx < 0) continue;

            var pool = EntryRegistry.CreatePool(ct) ?? new GenericPool();
            pool.Resize(maxIdx + 1);

            foreach (var e in ce)
                foreach (var comp in e.Components)
                    pool.SetRaw(e.AssignedIndex, comp.Key, comp.Value);

            pools[ct] = pool;

            foreach (var rec in EntryRegistry.All)
            {
                if (!rec.Concepts.Contains(ct)) continue;
                var found = entries.FirstOrDefault(e => e.Key == rec.EntryType.Name);
                if (found != null)
                    entryIndices[rec.EntryType] = found.AssignedIndex;
            }
        }

        ctx.World = WorldAbstractions.WorldFactory.Create(pools, entryIndices);
        ctx.Diagnostics.Info($"Built world with {pools.Count} concept pools");
    }

    private static Type? FindConceptType(string name)
    {
        foreach (var r in EntryRegistry.All)
            foreach (var c in r.Concepts)
                if (c.Name == name) return c;
        return null;
    }
}
