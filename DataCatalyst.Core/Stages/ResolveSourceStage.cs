using System;
using System.Collections.Generic;
using System.Linq;
using LoaderAbstractions = DataCatalyst.Loader;
using PipelineAbstractions = DataCatalyst.Pipeline;
using StageContext = DataCatalyst.Pipeline.PipelineContext;

namespace DataCatalyst.Stages;

internal sealed class ResolveSourceStage : PipelineAbstractions.IPipelineStage
{
    private readonly List<LoaderAbstractions.DataSource> _sources;

    public ResolveSourceStage(List<LoaderAbstractions.DataSource> sources)
    {
        _sources = sources;
    }

    public string Id => "ResolveSources";

    public void Execute(StageContext ctx)
    {
        // Build adjacency + in-degree map
        var map = new Dictionary<string, LoaderAbstractions.DataSource>();
        var inDegree = new Dictionary<string, int>();
        var edges = new Dictionary<string, List<string>>();

        foreach (var source in _sources)
        {
            map[source.Name] = source;
            inDegree[source.Name] = 0;
            edges[source.Name] = new List<string>();
        }

        foreach (var source in _sources)
        {
            foreach (var dep in source.DependsOn)
            {
                if (map.ContainsKey(dep))
                {
                    edges[dep].Add(source.Name);
                    inDegree[source.Name]++;
                }
                else
                {
                    ctx.Diagnostics.Warn($"Source '{source.Name}' depends on '{dep}' which is not registered");
                }
            }
        }

        // Kahn topological sort
        var ready = new List<string>(inDegree
            .Where(kv => kv.Value == 0)
            .OrderBy(kv => map[kv.Key].Priority)
            .ThenBy(kv => kv.Key)
            .Select(kv => kv.Key));

        var sorted = new List<LoaderAbstractions.DataSource>();

        while (ready.Count > 0)
        {
            var name = ready[0];
            ready.RemoveAt(0);
            sorted.Add(map[name]);

            foreach (var next in edges[name])
            {
                inDegree[next]--;
                if (inDegree[next] == 0)
                    ready.Add(next);
            }

            ready = ready.OrderBy(n => map[n].Priority).ThenBy(n => n).ToList();
        }

        // Store sorted sources in context bag
        ctx.Bag["SortedSources"] = sorted;
        ctx.Diagnostics.Info($"Resolved {sorted.Count} sources in dependency order");
    }
}
