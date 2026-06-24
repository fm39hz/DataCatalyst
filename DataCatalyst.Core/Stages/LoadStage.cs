using System;
using System.Collections.Generic;
using System.Linq;
using LoaderAbstractions = DataCatalyst.Loader;
using PipelineAbstractions = DataCatalyst.Pipeline;
using StageContext = DataCatalyst.Pipeline.PipelineContext;
using DataCatalyst.Storage;

namespace DataCatalyst.Stages;

internal sealed class LoadStage : PipelineAbstractions.IPipelineStage
{
    private readonly List<LoaderAbstractions.DataSource> _sources;

    public LoadStage(List<LoaderAbstractions.DataSource> sources)
    {
        _sources = sources;
    }

    public string Id => "Load";

    public void Execute(StageContext ctx)
    {
        var sorted = ctx.Bag.TryGetValue("SortedSources", out var s)
            ? s as List<LoaderAbstractions.DataSource> ?? _sources
            : _sources;

        var allEntries = new List<RawEntry>();

        foreach (var source in sorted)
        {
            var result = source.Loader.LoadDirectory(source.Path);

            foreach (var diag in result.Diagnostics)
                ctx.Diagnostics.Warn($"[{source.Name}] {diag}");

            // Result entries come as RawEntry from JSON loader
            if (result.Entries.Count > 0 && result.Entries[0] is RawEntry re)
            {
                foreach (var entry in result.Entries.Cast<RawEntry>())
                {
                    entry.AssignedIndex = allEntries.Count;
                    allEntries.Add(entry);
                }
            }

            ctx.Diagnostics.Info($"Loaded {result.Entries.Count} entries from '{source.Name}'");
        }

        ctx.Bag["RawEntries"] = allEntries;
        ctx.Bag["AllKeys"] = new HashSet<string>(allEntries.Select(e => e.Key));
    }
}
