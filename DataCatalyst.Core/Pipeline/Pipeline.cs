using System;
using System.Collections.Generic;
using DataCatalyst.Loader;
using DataCatalyst.Pipeline;
using DataCatalyst.Registry;
using WorldAbstractions = DataCatalyst.World;

namespace DataCatalyst.Pipeline;

public sealed class Pipeline
{
    private readonly List<DataSource> _sources = new();
    private readonly IPipelineStage[] _stages;

    /// <summary>
    /// Creates a Pipeline. If stages is null, uses default stage sequence.
    /// </summary>
    public Pipeline(IPipelineStage[]? stages = null)
    {
        _stages = stages ?? Array.Empty<IPipelineStage>();
    }

    /// <summary>Default pipeline stage sequence.</summary>
    public static IPipelineStage[] DefaultStages(List<DataSource> sources) => new IPipelineStage[]
    {
        new Stages.ResolveSourceStage(sources),
        new Stages.LoadStage(sources),
        new Stages.MergeStage(),
        new Stages.InheritanceStage(),
        new Stages.ResolveCrossRefStage(),
        new Stages.BuildWorldStage(),
    };

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
        var stages = _stages.Length > 0 ? _stages : DefaultStages(_sources);

        foreach (var stage in stages)
        {
            stage.Execute(ctx);
            if (ctx.Diagnostics.HasErrors)
            {
                PipeDiag(ctx, diagnostics);
                return null!;
            }
        }

        PipeDiag(ctx, diagnostics);
        EntryRegistry.Freeze();
        return ctx.World ?? throw new InvalidOperationException("Pipeline build produced no World");
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
