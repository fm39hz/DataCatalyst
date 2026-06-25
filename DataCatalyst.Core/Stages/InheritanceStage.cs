using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Pipeline;
using DataCatalyst.Storage;

namespace DataCatalyst.Stages;

internal sealed class InheritanceStage : IPipelineStage
{
    public string Id => "Inherit";

    public void Execute(PipelineContext ctx)
    {
        var entries = ctx.RawEntries;
        if (entries == null) return;

        var byKey = entries.ToDictionary(e => e.Key);
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        void Resolve(RawEntry entry)
        {
            if (entry.Inherits == null || visited.Contains(entry.Key)) return;
            if (visiting.Contains(entry.Key))
            { ctx.Diagnostics.Error($"Cycle detected: {entry.Key}"); return; }

            visiting.Add(entry.Key);

            if (!byKey.TryGetValue(entry.Inherits, out var parent))
            {
                ctx.Diagnostics.Warn($"Entry '{entry.Key}' inherits from missing '{entry.Inherits}'");
                visiting.Remove(entry.Key); visited.Add(entry.Key); return;
            }

            Resolve(parent);

            foreach (var kv in parent.RawFields)
                if (!entry.RawFields.ContainsKey(kv.Key))
                {
                    entry.RawFields[kv.Key] = kv.Value;
                    if (!entry.FieldNames.Contains(kv.Key)) entry.FieldNames.Add(kv.Key);
                }

            visiting.Remove(entry.Key); visited.Add(entry.Key);
        }

        foreach (var entry in entries) Resolve(entry);
    }
}
