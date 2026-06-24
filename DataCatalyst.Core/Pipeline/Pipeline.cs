using System;
using System.Collections.Generic;
using LoaderAbstractions = DataCatalyst.Loader;
using PipelineAbstractions = DataCatalyst.Pipeline;
using WorldAbstractions = DataCatalyst.World;
using DataCatalyst;
using DataCatalyst.Registry;

namespace DataCatalyst.Pipeline;

public sealed class Pipeline
{
    private readonly List<LoaderAbstractions.DataSource> _sources = new();
    private readonly List<(PipelineAbstractions.IPipelineStage Stage, PipelineAbstractions.StagePosition Position)> _userStages = new();
    private static readonly PipelineAbstractions.StagePosition[] AllHooks = {
        PipelineAbstractions.StagePosition.AfterLoad,
        PipelineAbstractions.StagePosition.AfterMerge,
        PipelineAbstractions.StagePosition.AfterResolve,
        PipelineAbstractions.StagePosition.BeforeBuild,
    };

    public Pipeline AddSource(string name, LoaderAbstractions.IDataLoader loader, string path,
        Action<LoaderAbstractions.DataSource>? configure = null)
    {
        var source = new LoaderAbstractions.DataSource(name, loader, path);
        configure?.Invoke(source);
        _sources.Add(source);
        return this;
    }

    public Pipeline AddStage(PipelineAbstractions.IPipelineStage stage,
        PipelineAbstractions.StagePosition position)
    {
        _userStages.Add((stage, position));
        return this;
    }

    public WorldAbstractions.World Build(out DiagnosticBag diagnostics)
    {
        diagnostics = new DiagnosticBag();
        var ctx = new PipelineAbstractions.PipelineContext();

        // Build stage sequence
        var stages = BuildStageSequence();

        foreach (var stage in stages)
        {
            stage.Execute(ctx);
            if (ctx.Diagnostics.HasErrors)
            {
                foreach (var d in ctx.Diagnostics.Items)
                    diagnostics.Error(d);
                return null!;
            }
        }

        foreach (var d in ctx.Diagnostics.Items)
        {
            if (d.StartsWith("[Error]")) diagnostics.Error(d.Substring(7));
            else if (d.StartsWith("[Warn]")) diagnostics.Warn(d.Substring(7));
            else diagnostics.Info(d.Substring(6));
        }

        EntryRegistry.Freeze();
        return ctx.World ?? throw new InvalidOperationException("Pipeline build produced no World");
    }

    private List<PipelineAbstractions.IPipelineStage> BuildStageSequence()
    {
        var result = new List<PipelineAbstractions.IPipelineStage>();

        // Default stages
        result.Add(new Stages.ResolveSourceStage(_sources));
        result.Add(new Stages.LoadStage(_sources));

        // AfterLoad hook
        AddUserStagesAt(result, PipelineAbstractions.StagePosition.AfterLoad);

        result.Add(new Stages.MergeStage());
        result.Add(new Stages.InheritanceStage());
        result.Add(new Stages.ResolveCrossRefStage());

        // AfterResolve hook
        AddUserStagesAt(result, PipelineAbstractions.StagePosition.AfterResolve);

        result.Add(new Stages.BuildWorldStage());

        // BeforeBuild hook
        AddUserStagesAt(result, PipelineAbstractions.StagePosition.BeforeBuild);

        return result;
    }

    private void AddUserStagesAt(List<PipelineAbstractions.IPipelineStage> list,
        PipelineAbstractions.StagePosition position)
    {
        foreach (var (stage, pos) in _userStages)
            if (pos == position)
                list.Add(stage);
    }
}
