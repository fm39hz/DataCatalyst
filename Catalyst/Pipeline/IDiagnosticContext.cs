namespace Catalyst.Pipeline;

using Catalyst;

public interface IDiagnosticContext {
	public DiagnosticBag Diagnostics { get; }
}
