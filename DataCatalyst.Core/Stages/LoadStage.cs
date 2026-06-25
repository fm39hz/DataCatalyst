using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Pipeline;
using DataCatalyst.Storage;
using LoaderAbstractions = DataCatalyst.Loader;

namespace DataCatalyst.Stages;

internal sealed class LoadStage : IPipelineStage
{
    private readonly List<LoaderAbstractions.DataSource> _sources;
    public LoadStage(List<LoaderAbstractions.DataSource> sources) => _sources = sources;
    public string Id => "Load";

    public void Execute(PipelineContext ctx)
    {
        var sorted = ctx.SortedSources ?? _sources;
        var allEntries = new List<RawEntry>();
        var allKeys = new HashSet<string>();

        foreach (var source in sorted)
        {
            var result = source.Loader.LoadDirectory(source.Path);
            foreach (var diag in result.Diagnostics)
                ctx.Diagnostics.Warn($"[{source.Name}] {diag}");

            var policyValue = (int)source.MergePolicy;
            foreach (var e in result.Entries)
            {
                if (e is RawEntry re)
                {
                    re.MergePolicyValue = policyValue;
                    re.AssignedIndex = allEntries.Count;
                    allEntries.Add(re);
                    allKeys.Add(re.Key);
                }
            }

            ctx.Diagnostics.Info($"Loaded {result.Entries.Count} entries from '{source.Name}'");
        }

        ctx.RawEntries = allEntries;
        ctx.AllKeys = allKeys;
    }
}
