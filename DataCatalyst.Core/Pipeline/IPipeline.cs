namespace DataCatalyst.Pipeline;

using DataCatalyst.Knowledge;

public interface IPipeline {
    public Knowledge? Run(out DiagnosticBag diagnostics);
}
