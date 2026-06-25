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
    readonly List<DataSource> _sources = new();
    readonly SchemaRegistry _schema = new();
    public SchemaRegistry Schema => _schema;

    public Pipeline LoadSchemaFrom(ISchemaLoader loader, string path)
    { _schema.MergeFrom(loader.LoadSchemaDirectory(path)); return this; }

    public Pipeline AddSource(string name, IDataLoader loader, string path,
        Action<DataSource>? configure = null)
    { var s = new DataSource(name, loader, path); configure?.Invoke(s); _sources.Add(s); return this; }

    public WorldAbstractions.World Build(out DiagnosticBag diagnostics)
    {
        diagnostics = new DiagnosticBag();
        var ctx = new PipelineContext();
        ResolveSources(ctx); if (ctx.Diagnostics.HasErrors) { PipeDiag(ctx, diagnostics); return null!; }
        Load(ctx);           if (ctx.Diagnostics.HasErrors) { PipeDiag(ctx, diagnostics); return null!; }
        Merge(ctx);          if (ctx.Diagnostics.HasErrors) { PipeDiag(ctx, diagnostics); return null!; }
        Inherit(ctx);        if (ctx.Diagnostics.HasErrors) { PipeDiag(ctx, diagnostics); return null!; }
        ResolveIDs(ctx);     if (ctx.Diagnostics.HasErrors) { PipeDiag(ctx, diagnostics); return null!; }
        ResolveCrossRefs(ctx); if (ctx.Diagnostics.HasErrors) { PipeDiag(ctx, diagnostics); return null!; }
        BuildWorld(ctx);     if (ctx.Diagnostics.HasErrors) { PipeDiag(ctx, diagnostics); return null!; }
        PipeDiag(ctx, diagnostics);
        EntryRegistry.Freeze();
        return ctx.World ?? throw new InvalidOperationException("Build produced no World");
    }

    void ResolveSources(PipelineContext ctx) {
        var map = new Dictionary<string, DataSource>();
        var inDeg = new Dictionary<string, int>();
        var edges = new Dictionary<string, List<string>>();
        foreach (var s in _sources) { map[s.Name] = s; inDeg[s.Name] = 0; edges[s.Name] = new List<string>(); }
        foreach (var s in _sources)
            foreach (var d in s.DependsOn)
                if (map.ContainsKey(d)) { edges[d].Add(s.Name); inDeg[s.Name]++; }
                else ctx.Diagnostics.Warn($"'{s.Name}' depends on '{d}' not found");
        var ready = new List<string>(inDeg.Where(kv => kv.Value == 0)
            .OrderBy(kv => map[kv.Key].Priority).ThenBy(kv => kv.Key).Select(kv => kv.Key));
        ctx.SortedSources = new List<DataSource>();
        while (ready.Count > 0) {
            var n = ready[0]; ready.RemoveAt(0); ctx.SortedSources.Add(map[n]);
            foreach (var next in edges[n]) if (--inDeg[next] == 0) ready.Add(next);
            ready = ready.OrderBy(x => map[x].Priority).ThenBy(x => x).ToList(); }
        ctx.Diagnostics.Info($"Resolved {ctx.SortedSources.Count} sources");
    }

    void Load(PipelineContext ctx) {
        var sorted = ctx.SortedSources ?? _sources;
        var all = new List<RawEntry>();
        var keys = new HashSet<string>();
        foreach (var src in sorted) {
            var result = src.Loader.LoadDirectory(src.Path);
            foreach (var d in result.Diagnostics) ctx.Diagnostics.Warn($"[{src.Name}] {d}");
            int pv = (int)src.MergePolicy;
            foreach (var e in result.Entries)
                if (e is RawEntry re)
                { re.MergePolicyValue = pv; re.AssignedIndex = all.Count; all.Add(re); keys.Add(re.Key); }
            ctx.Diagnostics.Info($"Loaded {result.Entries.Count} from '{src.Name}'");
        }
        ctx.RawEntries = all;
        ctx.AllKeys = keys;
    }

    void Merge(PipelineContext ctx) {
        var entries = ctx.RawEntries;
        if (entries == null || entries.Count == 0) return;
        const int FP = 1, OV = 2, RP = 3;
        var merged = new Dictionary<string, RawEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries) {
            if (!merged.TryGetValue(e.Key, out var ex)) { merged[e.Key] = e; continue; }
            switch (e.MergePolicyValue) {
                case RP: DoReplace(ex, e); break;
                case OV: DoOverlay(ex, e); break;
                case FP: DoFieldPatch(ex, e); break;
                default: DoPatch(ex, e);   break;
            }
        }
        var final = new List<RawEntry>(merged.Values);
        for (int i = 0; i < final.Count; i++) final[i].AssignedIndex = i;
        ctx.RawEntries = final;
    }

    void Inherit(PipelineContext ctx) {
        var entries = ctx.RawEntries;
        if (entries == null) return;
        var byKey = entries.ToDictionary(e => e.Key);
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();
        void Resolve(RawEntry e) {
            if (e.Inherits == null || visited.Contains(e.Key)) return;
            if (visiting.Contains(e.Key)) { ctx.Diagnostics.Error($"Cycle: {e.Key}"); return; }
            visiting.Add(e.Key);
            if (!byKey.TryGetValue(e.Inherits, out var p))
            { ctx.Diagnostics.Warn($"Missing parent '{e.Inherits}'"); visiting.Remove(e.Key); visited.Add(e.Key); return; }
            Resolve(p);
            foreach (var kv in p.RawFields)
                if (!e.RawFields.ContainsKey(kv.Key))
                { e.RawFields[kv.Key] = kv.Value; if (!e.FieldNames.Contains(kv.Key)) e.FieldNames.Add(kv.Key); }
            visiting.Remove(e.Key); visited.Add(e.Key);
        }
        foreach (var e in entries) Resolve(e);
    }

    void ResolveIDs(PipelineContext ctx) {
        var raw = ctx.RawEntries;
        if (raw == null || raw.Count == 0) return;
        ctx.Entries.Clear();
        foreach (var re in raw) {
            var concepts = new List<int>();
            var conceptSet = new HashSet<int>();
            foreach (var c in re.Concepts) {
                var id = _schema.GetConceptId(c);
                if (id < 0) { ctx.Diagnostics.Warn($"Unknown concept '{c}' in '{re.Key}'"); continue; }
                concepts.Add(id);
                conceptSet.Add(id);
            }
            var aspects = new Dictionary<int, object?>();
            foreach (var kv in re.RawFields) {
                var id = _schema.GetAspectId(kv.Key);
                if (id >= 0) aspects[id] = kv.Value;
            }
            ctx.Entries.Add(new ResolvedEntry {
                Key = re.Key, AssignedIndex = re.AssignedIndex, Inherits = re.Inherits,
                Concepts = concepts, ConceptSet = conceptSet, AspectFields = aspects,
            });
        }
        ctx.RawEntries.Clear();
    }

    void ResolveCrossRefs(PipelineContext ctx) {
        var entries = ctx.Entries;
        if (entries == null || entries.Count == 0) return;
        foreach (var entry in entries) {
            foreach (var kv in entry.AspectFields) {
                // Resolve $ref in values
                bool mod = false;
                var resolved = WalkNode(kv.Value, ctx.AllKeys, ref mod, entry.Key, kv.Key.ToString(), ctx);
                entry.AspectFields[kv.Key] = resolved;
            }
            foreach (var kv in entry.AspectFields) {
                var raw = kv.Value;
                var aname = _schema.TryGetAspectName(kv.Key, out var n) ? n : null;
                if (aname == null) continue;
                if (AspectTypeRegistry.TryGetType(aname, out var t))
                    try { var d = AspectTypeRegistry.Deserialize(t, raw); if (d != null) entry.Components[t] = d; }
                    catch (Exception ex) { ctx.Diagnostics.Error($"Deserialize '{aname}': {ex.Message}"); }
                else
                    ctx.Diagnostics.Warn($"Unknown aspect '{aname}' (ID {kv.Key}) for '{entry.Key}' — runtime dynamic");
            }
        }
    }

    void BuildWorld(PipelineContext ctx) {
        var entries = ctx.Entries;
        if (entries == null || entries.Count == 0) { ctx.Diagnostics.Error("No entries"); return; }
        var byConcept = new Dictionary<int, List<ResolvedEntry>>();
        var nameIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries) {
            nameIndex[e.Key] = e.AssignedIndex;
            foreach (var c in e.Concepts)
                (byConcept.TryGetValue(c, out var l) ? l : (byConcept[c] = new())).Add(e);
        }
        var pools = new Dictionary<Type, IStoragePool>();
        var entryIndices = new Dictionary<Type, int>();
        foreach (var kv in byConcept) {
            var ct = FindType(kv.Key);
            if (ct == null) { ctx.Diagnostics.Warn($"No type for concept ID '{kv.Key}'"); continue; }
            var ce = kv.Value;
            int maxIdx = ce.Max(e => e.AssignedIndex);
            if (maxIdx < 0) continue;
            var allowed = _schema.GetConceptAspects(kv.Key);
            bool hasDynamic = allowed != null && allowed.Any(a => _schema.TryGetAspectName(a, out var _an) && _an != null && !AspectTypeRegistry.HasType(_an));
            IStoragePool pool;
            if (hasDynamic) {
                var dp = new DynamicPool(); dp.Resize(maxIdx + 1);
                foreach (var e in ce)
                    foreach (var comp in e.Components)
                        if (allowed == null || allowed.Contains(_schema.GetAspectId(comp.Key.Name)))
                            dp.SetRaw(e.AssignedIndex, comp.Key, comp.Value);
                pool = dp;
            } else {
                pool = EntryRegistry.CreatePool(ct) ?? new GenericPool();
                pool.Resize(maxIdx + 1);
                foreach (var e in ce)
                    foreach (var comp in e.Components)
                        if (allowed == null || allowed.Contains(_schema.GetAspectId(comp.Key.Name)))
                            pool.SetRaw(e.AssignedIndex, comp.Key, comp.Value);
            }
            pools[ct] = pool;
            foreach (var rec in EntryRegistry.All) {
                if (!rec.Concepts.Contains(ct)) continue;
                var found = entries.FirstOrDefault(e => e.Key == rec.EntryType.Name);
                if (found != null) entryIndices[rec.EntryType] = found.AssignedIndex;
            }
        }
        ctx.World = WorldAbstractions.WorldFactory.Create(pools, entryIndices, _schema);
        ctx.Diagnostics.Info($"Built {pools.Count} concept pools");
    }

    Type? FindType(int conceptId) {
        if (_schema.TryGetConceptName(conceptId, out var name) && name != null)
            foreach (var r in EntryRegistry.All)
                foreach (var c in r.Concepts)
                    if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) return c;
        return null;
    }

    static object? WalkNode(object? n, HashSet<string> keys, ref bool mod, string ek, string an, PipelineContext ctx) {
        if (n == null) return null;
        if (n is Dictionary<string, object?> d) {
            if (d.TryGetValue("$ref", out var rv) && rv is string tk) {
                if (keys.Contains(tk)) ctx.Diagnostics.Info($"Resolved '{ek}.{an}' → '{tk}'");
                else ctx.Diagnostics.Warn($"Unresolved '{ek}.{an}' → '{tk}'");
                mod = true; return tk;
            }
            foreach (var k in d.Keys.ToList())
            { var v = d[k]; var r = WalkNode(v, keys, ref mod, ek, an + "." + k, ctx); if (r != v) { d[k] = r; mod = true; } }
            return d;
        }
        if (n is List<object?> list)
            for (int i = 0; i < list.Count; i++)
            { var v = list[i]; var r = WalkNode(v, keys, ref mod, ek, an + $"[{i}]", ctx); if (r != v) { list[i] = r; mod = true; } }
        return n;
    }

    static void DoReplace(RawEntry e, RawEntry n)
    { e.RawFields.Clear(); e.FieldNames.Clear(); e.Components.Clear(); e.Concepts = n.Concepts; e.ConceptSet = n.ConceptSet; e.Inherits = n.Inherits; foreach (var kv in n.RawFields) { e.RawFields[kv.Key] = kv.Value; e.FieldNames.Add(kv.Key); } }
    static void DoOverlay(RawEntry e, RawEntry n)
    { foreach (var kv in n.RawFields) { e.RawFields[kv.Key] = kv.Value; if (!e.FieldNames.Contains(kv.Key)) e.FieldNames.Add(kv.Key); } if (n.Inherits != null) e.Inherits = n.Inherits; }
    static void DoPatch(RawEntry e, RawEntry n)
    { foreach (var kv in n.RawFields) { e.RawFields[kv.Key] = DeepMerge(e.RawFields.GetValueOrDefault(kv.Key), kv.Value, false); if (!e.FieldNames.Contains(kv.Key)) e.FieldNames.Add(kv.Key); } if (n.Inherits != null) e.Inherits = n.Inherits; }
    static void DoFieldPatch(RawEntry e, RawEntry n)
    { foreach (var kv in n.RawFields) { e.RawFields[kv.Key] = DeepMerge(e.RawFields.GetValueOrDefault(kv.Key), kv.Value, true); if (!e.FieldNames.Contains(kv.Key)) e.FieldNames.Add(kv.Key); } if (n.Inherits != null) e.Inherits = n.Inherits; }

    static object? DeepMerge(object? x, object? y, bool append) {
        if (x == null || y == null) return y ?? x;
        if (x is Dictionary<string, object?> d1 && y is Dictionary<string, object?> d2)
        { foreach (var kv in d2) { d1.TryGetValue(kv.Key, out var s); d1[kv.Key] = DeepMerge(s, kv.Value, append); } return d1; }
        if (x is List<object?> l1 && y is List<object?> l2) { if (append) { l1.AddRange(l2); return l1; } return l2; }
        return y;
    }

    static void PipeDiag(PipelineContext ctx, DiagnosticBag d) {
        foreach (var msg in ctx.Diagnostics.Items) {
            if (msg.StartsWith("[Error]")) d.Error(msg.Substring(7));
            else if (msg.StartsWith("[Warn]")) d.Warn(msg.Substring(7));
            else d.Info(msg.Substring(6));
        }
    }
}
