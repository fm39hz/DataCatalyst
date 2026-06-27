namespace DataCatalyst.Pipeline;

public interface IPipelineStage {
	public string Name { get; }
	public void Execute(PipelineContext ctx);
}
