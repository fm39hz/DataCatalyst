namespace FM39hz.DataCatalyst.Plugins.Modding.Runtime;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FM39hz.DataCatalyst.Abstractions;

public static class ModLoader {
    private static readonly Regex _versionPattern = new(@"^(\d+)(?:\.(\d+))?(?:\.(\d+))?$", RegexOptions.Compiled);

    // ── Scan ──────────────────────────────────────────────

    public static ModLoadResult[] Scan(string modsDir) {
        if (!Directory.Exists(modsDir))
            return Array.Empty<ModLoadResult>();

        var results = new List<ModLoadResult>();
        foreach (var modDir in Directory.EnumerateDirectories(modsDir)) {
            ScanDirectory(modDir, results);
        }
        return results.ToArray();
    }

    public static ModLoadResult[] ScanAll(params ModSource[] sources) {
        var all = new List<ModLoadResult>();
        foreach (var source in sources) {
            if (!Directory.Exists(source.Directory)) continue;
            foreach (var modDir in Directory.EnumerateDirectories(source.Directory)) {
                ScanDirectory(modDir, all);
            }
        }
        return Dedupe(all).ToArray();
    }

    private static void ScanDirectory(string modDir, List<ModLoadResult> results) {
        var manifestPath = Path.Combine(modDir, "mod.json");
        if (!File.Exists(manifestPath)) {
            results.Add(new ModLoadResult(
                Path.GetFileName(modDir), false,
                new ScriptError(Path.GetFileName(modDir), "Missing mod.json")));
            return;
        }

        ModManifest? manifest;
        try {
            manifest = ParseManifest(manifestPath);
        } catch (Exception ex) {
            results.Add(new ModLoadResult(
                Path.GetFileName(modDir), false,
                new ScriptError(Path.GetFileName(modDir), $"Failed to parse mod.json: {ex.Message}", ex)));
            return;
        }

        var warnings = new List<ScriptError>();
        foreach (var entry in manifest.Content) {
            if (!File.Exists(Path.Combine(modDir, entry.File)))
                warnings.Add(new ScriptError(manifest.Id, $"Content file missing: {entry.File}"));
        }

        results.Add(new ModLoadResult(manifest.Id, true, warnings: warnings, manifest: manifest));
    }

    // ── Full lifecycle (content + init) ──────────────────

    public static ModLoadResult[] LoadAll(string modsDir, IScriptEngine engine,
        IComponentSchemaRegistry componentRegistry,
        IModServiceRegistry? serviceRegistry = null) {
        return LoadAllSources(
            new[] { ModSource.From(modsDir) },
            engine, componentRegistry, serviceRegistry);
    }

    public static ModLoadResult[] LoadAllSources(ModSource[] sources, IScriptEngine engine,
        IComponentSchemaRegistry componentRegistry,
        IModServiceRegistry? serviceRegistry = null) {

        var scanResults = ScanAll(sources);
        var sorted = TopoSort(scanResults);
        serviceRegistry ??= new ModServiceRegistry();

        // Phase 1: CONTENT — tất cả mods
        ProcessContentPhase(sorted, componentRegistry);

        // Phase 2: INIT — mỗi mod chạy script
        return ProcessInitPhase(sorted, engine, serviceRegistry);
    }

    // ── Phase 1: Content ─────────────────────────────────

    private static void ProcessContentPhase(
        IReadOnlyList<ModLoadResult> sorted,
        IComponentSchemaRegistry componentRegistry) {

        foreach (var result in sorted) {
            if (!result.Success || result.Manifest is null) continue;
            foreach (var entry in result.Manifest.Content) {
                switch (entry.Type) {
                    case "component":
                        LoadComponentSchema(entry, result.Manifest.Directory, componentRegistry);
                        break;
                    case "data":
                        // Data overrides load qua ItemMod.LoadMods
                        // Game code gọi riêng, không ở đây
                        break;
                }
            }
        }
    }

    // ── Phase 2: Init ─────────────────────────────────────

    private static ModLoadResult[] ProcessInitPhase(
        IReadOnlyList<ModLoadResult> sorted,
        IScriptEngine engine,
        IModServiceRegistry serviceRegistry) {

        var loaded = new List<ModLoadResult>();
        foreach (var result in sorted) {
            if (!result.Success || result.Manifest is null) {
                loaded.Add(result);
                continue;
            }

            try {
                using var ctx = engine.CreateContext(result.Manifest.Id);
                EntryExposerRegistry.ExposeAll(ctx);

                ctx.SetGlobal("ServiceRegistry", serviceRegistry);
                ctx.SetGlobal("__modId", result.Manifest.Id);

                foreach (var entry in result.Manifest.Content) {
                    if (entry.Type != "script") continue;
                    var path = System.IO.Path.Combine(result.Manifest.Directory, entry.File);
                    ctx.LoadFile(path);
                }

                loaded.Add(new ModLoadResult(result.Manifest.Id, true, manifest: result.Manifest));
            } catch (Exception ex) {
                loaded.Add(new ModLoadResult(
                    result.Manifest.Id, false,
                    new ScriptError(result.Manifest.Id, $"Init failed: {ex.Message}", ex)));
            }
        }
        return loaded.ToArray();
    }

    // ── Legacy — single-pass (kept for compat) ────────────

    public static ModLoadResult[] Load(string modsDir, IScriptEngine engine,
        IComponentSchemaRegistry componentRegistry) {
        return LoadAll(modsDir, engine, componentRegistry);
    }

    // ── Manifest parser ──────────────────────────────────

    private static ModManifest ParseManifest(string path) {
        var text = File.ReadAllText(path);
        var json = System.Text.Json.JsonDocument.Parse(text);
        var root = json.RootElement;

        var id = root.GetProperty("id").GetString() ?? throw new InvalidDataException("manifest missing 'id'");
        var version = ParseVersion(root.GetProperty("version").GetString() ?? "0.0.0");
        var gameVersion = root.TryGetProperty("gameVersion", out var gv)
            ? ParseVersion(gv.GetString() ?? "0.0.0")
            : new Version(0, 0, 0);

        var deps = new List<ModDependency>();
        if (root.TryGetProperty("dependencies", out var depsEl)) {
            foreach (var d in depsEl.EnumerateArray()) {
                var did = d.GetProperty("id").GetString() ?? "";
                var dver = d.TryGetProperty("version", out var dv) ? dv.GetString() ?? "0.0.0" : "0.0.0";
                deps.Add(new ModDependency(did, dver));
            }
        }

        var content = new List<ModContentEntry>();
        if (root.TryGetProperty("content", out var contentEl)) {
            foreach (var c in contentEl.EnumerateArray()) {
                var type = c.GetProperty("type").GetString() ?? "data";
                var file = c.GetProperty("file").GetString() ?? "";
                var target = c.TryGetProperty("target", out var t) ? t.GetString() : null;
                content.Add(new ModContentEntry(type, file, target));
            }
        }

        return new ModManifest(id, version, gameVersion, deps, content, Path.GetDirectoryName(path) ?? "");
    }

    // ── Component schema loader ───────────────────────────

    private static void LoadComponentSchema(ModContentEntry entry, string modDir, IComponentSchemaRegistry registry) {
        var path = System.IO.Path.Combine(modDir, entry.File);
        var text = File.ReadAllText(path);
        var json = System.Text.Json.JsonDocument.Parse(text);
        foreach (var prop in json.RootElement.EnumerateObject()) {
            var schema = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(prop.Value.GetRawText());
            if (schema is null) continue;

            var fields = new List<ComponentSchemaField>();
            foreach (var kv in schema) {
                fields.Add(new ComponentSchemaField(kv.Key, kv.Value));
            }
            registry.Register(new ComponentSchema(prop.Name, fields));
        }
    }

    // ── Utils ─────────────────────────────────────────────

    private static List<ModLoadResult> Dedupe(List<ModLoadResult> results) {
        var seen = new HashSet<string>();
        var deduped = new List<ModLoadResult>(results.Count);
        for (var i = results.Count - 1; i >= 0; i--) {
            if (results[i].Manifest is null) { deduped.Insert(0, results[i]); continue; }
            if (seen.Add(results[i].Manifest.Id))
                deduped.Insert(0, results[i]);
        }
        return deduped;
    }

    private static Version ParseVersion(string raw) {
        var m = _versionPattern.Match(raw);
        if (!m.Success) return new Version(0, 0, 0);
        var major = int.Parse(m.Groups[1].Value);
        var minor = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
        var patch = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
        return new Version(major, minor, patch);
    }

    // ── Topological sort ──────────────────────────────────

    private static IReadOnlyList<ModLoadResult> TopoSort(IReadOnlyList<ModLoadResult> results) {
        var map = new Dictionary<string, ModLoadResult>();
        var indegree = new Dictionary<string, int>();
        var edges = new Dictionary<string, List<string>>();

        foreach (var r in results) {
            if (!r.Success || r.Manifest is null) continue;
            map[r.Manifest.Id] = r;
            indegree[r.Manifest.Id] = 0;
            edges[r.Manifest.Id] = new List<string>();
        }

        foreach (var r in results) {
            if (!r.Success || r.Manifest is null) continue;
            foreach (var dep in r.Manifest.Dependencies) {
                if (!map.ContainsKey(dep.Id)) continue;
                edges[dep.Id].Add(r.Manifest.Id);
                indegree[r.Manifest.Id]++;
            }
        }

        var ready = new List<ModLoadResult>();
        foreach (var r in results) {
            if (!r.Success || r.Manifest is null) continue;
            if (indegree[r.Manifest.Id] == 0) ready.Add(r);
        }

        var sorted = new List<ModLoadResult>();
        while (ready.Count > 0) {
            var cur = ready[0]; ready.RemoveAt(0);
            sorted.Add(cur);
            if (cur.Manifest is null) continue;
            foreach (var child in edges[cur.Manifest.Id]) {
                indegree[child]--;
                if (indegree[child] == 0 && map.TryGetValue(child, out var next))
                    ready.Add(next);
            }
        }

        foreach (var r in results) {
            if (r.Success && r.Manifest is not null && !sorted.Contains(r))
                sorted.Add(r);
        }

        return sorted;
    }
}
