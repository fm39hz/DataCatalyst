using System;
using System.Collections.Generic;
using DataCatalyst.Pipeline;
using DataCatalyst.Storage;

namespace DataCatalyst.Stages;

internal sealed class MergeStage : IPipelineStage
{
    private const int PolicyPatch = 0, PolicyFieldPatch = 1, PolicyOverlay = 2, PolicyReplace = 3;

    public string Id => "Merge";

    public void Execute(PipelineContext ctx)
    {
        var entries = ctx.RawEntries;
        if (entries == null || entries.Count == 0) return;

        var merged = new Dictionary<string, RawEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (!merged.TryGetValue(entry.Key, out var existing))
            { merged[entry.Key] = entry; continue; }

            switch (entry.MergePolicyValue)
            {
                case PolicyReplace:  Replace(existing, entry);   break;
                case PolicyOverlay:  Overlay(existing, entry);   break;
                case PolicyFieldPatch: FieldPatch(existing, entry); break;
                default:             Patch(existing, entry);     break;
            }
            ctx.Diagnostics.Info($"Merged source into '{entry.Key}'");
        }

        var finalList = new List<RawEntry>(merged.Values);
        for (int i = 0; i < finalList.Count; i++) finalList[i].AssignedIndex = i;
        ctx.RawEntries = finalList;
    }

    private static void Replace(RawEntry e, RawEntry n)
    {
        e.RawFields.Clear(); e.FieldNames.Clear(); e.Components.Clear();
        e.Concepts = n.Concepts; e.ConceptSet = n.ConceptSet; e.Inherits = n.Inherits;
        foreach (var kv in n.RawFields) { e.RawFields[kv.Key] = kv.Value; e.FieldNames.Add(kv.Key); }
    }

    private static void Overlay(RawEntry e, RawEntry n)
    {
        foreach (var kv in n.RawFields)
        { e.RawFields[kv.Key] = kv.Value; if (!e.FieldNames.Contains(kv.Key)) e.FieldNames.Add(kv.Key); }
        if (n.Inherits != null) e.Inherits = n.Inherits;
    }

    private static void Patch(RawEntry e, RawEntry n)
    {
        foreach (var kv in n.RawFields)
        { e.RawFields[kv.Key] = DeepMerge(e.RawFields.GetValueOrDefault(kv.Key), kv.Value, false); if (!e.FieldNames.Contains(kv.Key)) e.FieldNames.Add(kv.Key); }
        if (n.Inherits != null) e.Inherits = n.Inherits;
    }

    private static void FieldPatch(RawEntry e, RawEntry n)
    {
        foreach (var kv in n.RawFields)
        { e.RawFields[kv.Key] = DeepMerge(e.RawFields.GetValueOrDefault(kv.Key), kv.Value, true); if (!e.FieldNames.Contains(kv.Key)) e.FieldNames.Add(kv.Key); }
        if (n.Inherits != null) e.Inherits = n.Inherits;
    }

    private static object? DeepMerge(object? x, object? y, bool append)
    {
        if (x == null || y == null) return y ?? x;
        if (x is Dictionary<string, object?> d1 && y is Dictionary<string, object?> d2)
        {
            foreach (var kv in d2) { d1.TryGetValue(kv.Key, out var s); d1[kv.Key] = DeepMerge(s, kv.Value, append); }
            return d1;
        }
        if (x is List<object?> l1 && y is List<object?> l2) return append ? (object?)Append(l1, l2) : l2;
        return y;
    }

    private static List<object?> Append(List<object?> t, List<object?> s) { t.AddRange(s); return t; }
}
