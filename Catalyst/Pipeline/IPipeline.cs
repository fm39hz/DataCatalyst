namespace Catalyst.Pipeline;

using Catalyst.Knowledge;

public interface IPipeline {
	public Knowledge Build(out DiagnosticBag diagnostics);
}
