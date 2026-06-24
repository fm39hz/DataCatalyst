using System;
using System.Collections.Generic;
using System.Linq;
using PipelineAbstractions = DataCatalyst.Pipeline;
using StageContext = DataCatalyst.Pipeline.PipelineContext;
using DataCatalyst.Storage;

namespace DataCatalyst.Stages;

internal sealed class InheritanceStage : PipelineAbstractions.IPipelineStage
{
    public string Id => "Inherit";

    public void Execute(StageContext ctx)
    {
        var entries = ctx.Bag["RawEntries"] as List<RawEntry>;
        if (entries == null) return;

        var byKey = entries.ToDictionary(e => e.Key);
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        void Resolve(RawEntry entry)
        {
            if (entry.Inherits == null) return;
            if (visiting.Contains(entry.Key))
            {
                ctx.Diagnostics.Error($"Cycle detected: {entry.Key}");
                return;
            }
            if (visited.Contains(entry.Key)) return;

            visiting.Add(entry.Key);

            if (!byKey.TryGetValue(entry.Inherits, out var parent))
            {
                ctx.Diagnostics.Warn($"Entry '{entry.Key}' inherits from missing '{entry.Inherits}'");
                visiting.Remove(entry.Key);
                visited.Add(entry.Key);
                return;
            }

            Resolve(parent);

            // CopyMissing: parent aspect → child if not present in _rawFields
            foreach (var kv in parent._rawFields)
            {
                if (!entry._rawFields.ContainsKey(kv.Key))
                {
                    entry._rawFields[kv.Key] = kv.Value;
                    if (!entry._fieldNames.Contains(kv.Key))
                        entry._fieldNames.Add(kv.Key);
                }
            }

            visiting.Remove(entry.Key);
            visited.Add(entry.Key);
        }

        foreach (var entry in entries)
            Resolve(entry);
    }
}
