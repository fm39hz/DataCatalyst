using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Loader;
using DataCatalyst.Pipeline;
using DataCatalyst.Registry;
using DataCatalyst.Schema;
using DataCatalyst.Storage;
using WorldAbstractions = DataCatalyst.World;

namespace DataCatalyst.Pipeline;

public sealed class Pipeline
{
    private readonly List<DataSource> _sources = new();
    private readonly SchemaRegistry? _schema;

    public Pipeline(SchemaRegistry? schema = null)
    {
        _schema = schema;
    }

    public Pipeline AddSource(string name, IDataLoader loader, string path,
        Action<DataSource>? configure = null)
    {
        var source = new DataSource(name, loader, path);
        configure?.Invoke(source);
        _sources.Add(source);
        return this;
    }

    public WorldAbstractions.World Build(out DiagnosticBag diagnostics)
    {
        diagnostics = new DiagnosticBag();
        var ctx = new PipelineContext();

        // 1. Resolve sources (topo sort)
        ResolveSources(ctx);
        if (ctx.Diagnostics.HasErrors) { PipeDiag(ctx, diagnostics); return null!; }

        // 2. Load entries
        Load(ctx);
        if (ctx.Diagnostics.HasErrors) { PipeDiag(ctx, diagnostics); return null!; }

        // 3. Merge
        Merge(ctx);
        if (ctx.Diagnostics.HasErrors) { PipeDiag(ctx, diagnostics); return null!; }

        // 4. Inherit
        Inherit(ctx);
        if (ctx.Diagnostics.HasErrors) { PipeDiag(ctx, diagnostics); return null!; }

        // 5. Resolve cross-refs + deserialize
        ResolveCrossRefs(ctx);
        if (ctx.Diagnostics.HasErrors) { PipeDiag(ctx, diagnostics); return null!; }

        // 6. Build World
        BuildWorld(ctx);
        if (ctx.Diagnostics.HasErrors) { PipeDiag(ctx, diagnostics); return null!; }

        PipeDiag(ctx, diagnostics);
        EntryRegistry.Freeze();
        return ctx.World ?? throw new InvalidOperationException("Build produced no World");
    }

    // ─── Pipeline steps (inlined, no IPipelineStage abstraction) ───

    private void ResolveSources(PipelineContext ctx)
    {
        var map = new Dictionary<string, DataSource>();
        var inDegree = new Dictionary<string, int>();
        var edges = new Dictionary<string, List<string>>();

        foreach (var s in _sources)
        { map[s.Name] = s; inDegree[s.Name] = 0; edges[s.Name] = new List<string>(); }

        foreach (var s in _sources)
            foreach (var dep in s.DependsOn)
                if (map.ContainsKey(dep)) { edges[dep].Add(s.Name); inDegree[s.Name]++; }
                else ctx.Diagnostics.Warn($"Source '{s.Name}' depends on '{dep}' not registered");

        var ready = new List<string>(inDegree.Where(kv => kv.Value == 0)
            .OrderBy(kv => map[kv.Key].Priority).ThenBy(kv => kv.Key).Select(kv => kv.Key));

        ctx.SortedSources = new List<DataSource>();
        while (ready.Count > 0)
        {
            var name = ready[0]; ready.RemoveAt(0);
            ctx.SortedSources.Add(map[name]);
            foreach (var next in edges[name])
                if (--inDegree[next] == 0) ready.Add(next);
            ready = ready.OrderBy(n => map[n].Priority).ThenBy(n => n).ToList();
        }
        ctx.Diagnostics.Info($"Resolved {ctx.SortedSources.Count} sources");
    }

    private void Load(PipelineContext ctx)
    {
        var sorted = ctx.SortedSources ?? _sources;
        var all = new List<RawEntry>();
        var keys = new HashSet<string>();

        foreach (var src in sorted)
        {
            var result = src.Loader.LoadDirectory(src.Path);
            foreach (var d in result.Diagnostics) ctx.Diagnostics.Warn($"[{src.Name}] {d}");

            int policy = (int)src.MergePolicy;
            foreach (var e in result.Entries)
                if (e is RawEntry re)
                { re.MergePolicyValue = policy; re.AssignedIndex = all.Count; all.Add(re); keys.Add(re.Key); }

            ctx.Diagnostics.Info($"Loaded {result.Entries.Count} from '{src.Name}'");
        }

        ctx.RawEntries = all;
        ctx.AllKeys = keys;
    }

    private void Merge(PipelineContext ctx)
    {
        var entries = ctx.RawEntries;
        if (entries == null || entries.Count == 0) return;

        const int FPATCH = 1, OVER = 2, REP = 3;
        var merged = new Dictionary<string, RawEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in entries)
        {
            if (!merged.TryGetValue(e.Key, out var ex)) { merged[e.Key] = e; continue; }
            switch (e.MergePolicyValue)
            {
                case REP:  DoReplace(ex, e); break;
                case OVER:  DoOverlay(ex, e); break;
                case FPATCH: DoFieldPatch(ex, e); break;
                default:       DoPatch(ex, e);   break;
            }
            ctx.Diagnostics.Info($"Merged '{e.Key}'");
        }

        var final = new List<RawEntry>(merged.Values);
        for (int i = 0; i < final.Count; i++) final[i].AssignedIndex = i;
        ctx.RawEntries = final;
    }

    private void Inherit(PipelineContext ctx)
    {
        var entries = ctx.RawEntries;
        if (entries == null) return;

        var byKey = entries.ToDictionary(e => e.Key);
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        void Resolve(RawEntry e)
        {
            if (e.Inherits == null || visited.Contains(e.Key)) return;
            if (visiting.Contains(e.Key)) { ctx.Diagnostics.Error($"Cycle: {e.Key}"); return; }
            visiting.Add(e.Key);
            if (!byKey.TryGetValue(e.Inherits, out var p))
            { ctx.Diagnostics.Warn($"Missing parent '{e.Inherits}' for '{e.Key}'"); visiting.Remove(e.Key); visited.Add(e.Key); return; }
            Resolve(p);
            foreach (var kv in p.RawFields)
                if (!e.RawFields.ContainsKey(kv.Key))
                { e.RawFields[kv.Key] = kv.Value; if (!e.FieldNames.Contains(kv.Key)) e.FieldNames.Add(kv.Key); }
            visiting.Remove(e.Key); visited.Add(e.Key);
        }
        foreach (var e in entries) Resolve(e);
    }

    private void ResolveCrossRefs(PipelineContext ctx)
    {
        var entries = ctx.RawEntries;
        var keys = ctx.AllKeys;
        if (entries == null || keys == null) return;

        // Resolve $ref → string + deserialize → Components
        foreach (var entry in entries)
        {
            foreach (var name in entry.RawFields.Keys.ToList())
            {
                bool mod = false;
                entry.RawFields[name] = ResolveNode(entry.RawFields[name], keys, ref mod, ctx, entry.Key, name);
            }
            foreach (var name in entry.RawFields.Keys)
            {
                var raw = entry.RawFields[name];
                if (AspectTypeRegistry.TryGetType(name, out var t))
                    try { var d = AspectTypeRegistry.Deserialize(t, raw); if (d != null) entry.Components[t] = d; }
                    catch (Exception ex) { ctx.Diagnostics.Error($"Deserialize '{name}' for '{entry.Key}': {ex.Message}"); }
                else
                    ctx.Diagnostics.Warn($"Unknown aspect '{name}' for '{entry.Key}' — runtime dynamic: stored as raw data");
            }
        }

        // Transition: RawEntry → ResolvedEntry
        ctx.Entries.Clear();
        foreach (var re in entries)
            ctx.Entries.Add(new ResolvedEntry
            {
                Key = re.Key, AssignedIndex = re.AssignedIndex,
                Inherits = re.Inherits, Concepts = re.Concepts, ConceptSet = re.ConceptSet,
                Components = re.Components, CrossRefs = re.CrossRefs,
            });
        ctx.RawEntries.Clear();
    }

    private void BuildWorld(PipelineContext ctx)
    {
        var entries = ctx.Entries;
        if (entries == null || entries.Count == 0) { ctx.Diagnostics.Error("No entries"); return; }

        var byConcept = new Dictionary<string, List<ResolvedEntry>>();
        foreach (var e in entries)
            foreach (var c in e.Concepts)
                (byConcept.TryGetValue(c, out var l) ? l : (byConcept[c] = new())).Add(e);

        // Determine pool aspects from schema or fallback to union
        var conceptAspects = _schema?.ConceptAspects;

        var pools = new Dictionary<Type, IStoragePool>();
        var entryIndices = new Dictionary<Type, int>();

        foreach (var kv in byConcept)
        {
            var ct = FindType(kv.Key);
            if (ct == null) { ctx.Diagnostics.Warn($"No type for concept '{kv.Key}'"); continue; }

            var ce = kv.Value;
            int maxIdx = ce.Max(e => e.AssignedIndex);
            if (maxIdx < 0) continue;

            // Determine allowed aspects for this concept
            var allowed = conceptAspects?.TryGetValue(kv.Key, out var a) == true ? a : null;

            var pool = EntryRegistry.CreatePool(ct) ?? new GenericPool();
            pool.Resize(maxIdx + 1);

            foreach (var e in ce)
                foreach (var comp in e.Components)
                    // If schema restricts, only store allowed aspects
                    if (allowed == null || allowed.Contains(comp.Key.Name))
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
        ctx.Diagnostics.Info($"Built {pools.Count} concept pools");
    }

    // ─── Helpers ───

    private static Type? FindType(string name)
    {
        foreach (var r in EntryRegistry.All)
            foreach (var c in r.Concepts)
                if (c.Name == name) return c;
        return null;
    }

    private static object? ResolveNode(object? node, HashSet<string> keys, ref bool mod, PipelineContext ctx, string ek, string an)
    {
        if (node == null) return null;
        if (node is Dictionary<string, object?> obj)
        {
            if (obj.TryGetValue("$ref", out var rv) && rv is string tk)
            {
                if (keys.Contains(tk)) ctx.Diagnostics.Info($"Resolved '{ek}.{an}' → '{tk}'");
                else ctx.Diagnostics.Warn($"Unresolved '{ek}.{an}' → '{tk}'");
                mod = true; return tk;
            }
            foreach (var k in obj.Keys.ToList())
            { var v = obj[k]; var r = ResolveNode(v, keys, ref mod, ctx, ek, an + "." + k); if (r != v) { obj[k] = r; mod = true; } }
            return obj;
        }
        if (node is List<object?> list)
            for (int i = 0; i < list.Count; i++)
            { var v = list[i]; var r = ResolveNode(v, keys, ref mod, ctx, ek, an + $"[{i}]"); if (r != v) { list[i] = r; mod = true; } }
        return node;
    }

    private static void DoReplace(RawEntry e, RawEntry n) { e.RawFields.Clear(); e.FieldNames.Clear(); e.Components.Clear(); e.Concepts = n.Concepts; e.ConceptSet = n.ConceptSet; e.Inherits = n.Inherits; foreach (var kv in n.RawFields) { e.RawFields[kv.Key] = kv.Value; e.FieldNames.Add(kv.Key); } }
    private static void DoOverlay(RawEntry e, RawEntry n) { foreach (var kv in n.RawFields) { e.RawFields[kv.Key] = kv.Value; if (!e.FieldNames.Contains(kv.Key)) e.FieldNames.Add(kv.Key); } if (n.Inherits != null) e.Inherits = n.Inherits; }
    private static void DoPatch(RawEntry e, RawEntry n) { foreach (var kv in n.RawFields) { e.RawFields[kv.Key] = DeepMerge(e.RawFields.GetValueOrDefault(kv.Key), kv.Value, false); if (!e.FieldNames.Contains(kv.Key)) e.FieldNames.Add(kv.Key); } if (n.Inherits != null) e.Inherits = n.Inherits; }
    private static void DoFieldPatch(RawEntry e, RawEntry n) { foreach (var kv in n.RawFields) { e.RawFields[kv.Key] = DeepMerge(e.RawFields.GetValueOrDefault(kv.Key), kv.Value, true); if (!e.FieldNames.Contains(kv.Key)) e.FieldNames.Add(kv.Key); } if (n.Inherits != null) e.Inherits = n.Inherits; }

    private static object? DeepMerge(object? x, object? y, bool append)
    {
        if (x == null || y == null) return y ?? x;
        if (x is Dictionary<string, object?> d1 && y is Dictionary<string, object?> d2) { foreach (var kv in d2) { d1.TryGetValue(kv.Key, out var s); d1[kv.Key] = DeepMerge(s, kv.Value, append); } return d1; }
        if (x is List<object?> l1 && y is List<object?> l2) { if (append) { l1.AddRange(l2); return l1; } return l2; }
        return y;
    }

    private static void PipeDiag(PipelineContext ctx, DiagnosticBag d)
    {
        foreach (var msg in ctx.Diagnostics.Items)
        {
            if (msg.StartsWith("[Error]")) d.Error(msg.Substring(7));
            else if (msg.StartsWith("[Warn]")) d.Warn(msg.Substring(7));
            else d.Info(msg.Substring(6));
        }
    }
}
