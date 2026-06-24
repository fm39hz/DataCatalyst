namespace DataCatalyst.Pipeline;

public interface IPipelineStage
{
    string Id { get; }
    void Execute(PipelineContext context);
}
