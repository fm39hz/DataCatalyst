using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Pipeline;
using LoaderAbstractions = DataCatalyst.Loader;

namespace DataCatalyst.Stages;

internal sealed class ResolveSourceStage : IPipelineStage
{
    private readonly List<LoaderAbstractions.DataSource> _sources;
    public ResolveSourceStage(List<LoaderAbstractions.DataSource> sources) => _sources = sources;
    public string Id => "ResolveSources";

    public void Execute(PipelineContext ctx)
    {
        var map = new Dictionary<string, LoaderAbstractions.DataSource>();
        var inDegree = new Dictionary<string, int>();
        var edges = new Dictionary<string, List<string>>();

        foreach (var s in _sources)
        {
            map[s.Name] = s;
            inDegree[s.Name] = 0;
            edges[s.Name] = new List<string>();
        }

        foreach (var s in _sources)
            foreach (var dep in s.DependsOn)
                if (map.ContainsKey(dep))
                {
                    edges[dep].Add(s.Name);
                    inDegree[s.Name]++;
                }
                else
                    ctx.Diagnostics.Warn($"Source '{s.Name}' depends on '{dep}' not registered");

        var ready = new List<string>(inDegree
            .Where(kv => kv.Value == 0)
            .OrderBy(kv => map[kv.Key].Priority).ThenBy(kv => kv.Key)
            .Select(kv => kv.Key));

        ctx.SortedSources = new List<LoaderAbstractions.DataSource>();

        while (ready.Count > 0)
        {
            var name = ready[0];
            ready.RemoveAt(0);
            ctx.SortedSources.Add(map[name]);
            foreach (var next in edges[name])
                if (--inDegree[next] == 0) ready.Add(next);
            ready = ready.OrderBy(n => map[n].Priority).ThenBy(n => n).ToList();
        }

        ctx.Diagnostics.Info($"Resolved {ctx.SortedSources.Count} sources in dependency order");
    }
}
