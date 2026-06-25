using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Pipeline;
using DataCatalyst.Storage;

namespace DataCatalyst.Stages;

internal sealed class ResolveCrossRefStage : IPipelineStage
{
    public string Id => "ResolveCrossRef";

    public void Execute(PipelineContext ctx)
    {
        var rawEntries = ctx.RawEntries;
        var allKeys = ctx.AllKeys;
        if (rawEntries == null || allKeys == null) return;

        foreach (var entry in rawEntries)
        {
            // Walk raw fields and resolve $ref references
            foreach (var name in entry.RawFields.Keys.ToList())
            {
                bool mod = false;
                entry.RawFields[name] = ResolveRefs(entry.RawFields[name], allKeys, ref mod, ctx, entry.Key, name);
            }

            // Deserialize resolved raw objects into typed components
            foreach (var name in entry.RawFields.Keys)
            {
                var raw = entry.RawFields[name];
                if (AspectTypeRegistry.TryGetType(name, out var t))
                {
                    try
                    {
                        var d = AspectTypeRegistry.Deserialize(t, raw);
                        if (d != null) entry.Components[t] = d;
                    }
                    catch (Exception ex)
                    {
                        ctx.Diagnostics.Error($"Deserialize '{name}' for '{entry.Key}': {ex.Message}");
                    }
                }
            }
        }

        // Transition: RawEntry → ResolvedEntry (discard raw fields, keep typed components)
        ctx.Entries.Clear();
        foreach (var re in rawEntries)
        {
            var resolved = new ResolvedEntry
            {
                Key = re.Key,
                AssignedIndex = re.AssignedIndex,
                Inherits = re.Inherits,
                Concepts = re.Concepts,
                ConceptSet = re.ConceptSet,
                Components = re.Components,
                CrossRefs = re.CrossRefs,
            };
            ctx.Entries.Add(resolved);
        }

        // RawEntries no longer needed — clear to free memory
        ctx.RawEntries.Clear();
    }

    private static object? ResolveRefs(object? node, HashSet<string> keys, ref bool mod, PipelineContext ctx, string ek, string an)
    {
        if (node == null) return null;

        if (node is Dictionary<string, object?> obj)
        {
            if (obj.TryGetValue("$ref", out var rv) && rv is string tk)
            {
                if (keys.Contains(tk)) ctx.Diagnostics.Info($"Resolved cross-ref '{ek}.{an}' → '{tk}'");
                else ctx.Diagnostics.Warn($"Unresolved cross-ref '{ek}.{an}' → '{tk}'");
                mod = true; return tk;
            }
            foreach (var k in obj.Keys.ToList())
            { var v = obj[k]; var r = ResolveRefs(v, keys, ref mod, ctx, ek, an + "." + k); if (r != v) { obj[k] = r; mod = true; } }
            return obj;
        }

        if (node is List<object?> list)
            for (int i = 0; i < list.Count; i++)
            { var v = list[i]; var r = ResolveRefs(v, keys, ref mod, ctx, ek, an + $"[{i}]"); if (r != v) { list[i] = r; mod = true; } }

        return node;
    }
}
