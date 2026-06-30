namespace Catalyst.Pipeline;

using Catalyst.Knowledge;

public interface IPipeline {
    Knowledge Build(out DiagnosticBag diagnostics);
}