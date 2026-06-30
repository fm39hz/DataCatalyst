namespace Catalyst.Pipeline;

using Catalyst;

public interface IDiagnosticContext {
    DiagnosticBag Diagnostics { get; }
}
