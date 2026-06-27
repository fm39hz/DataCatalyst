namespace DataCatalyst.Pipeline;

using DataCatalyst.Registry;

public interface IPipelineStage {
	string Name { get; }
	void Execute(PipelineContext ctx);
}
