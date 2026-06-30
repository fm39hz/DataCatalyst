namespace Catalyst.Pipeline;

using Catalyst.Knowledge;

public interface IPipeline {
    public Knowledge? Run(out DiagnosticBag diagnostics);
}
