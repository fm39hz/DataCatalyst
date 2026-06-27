namespace DataCatalyst.Pipeline;

using DataCatalyst;

public interface IDiagnosticContext {
    DiagnosticBag Diagnostics { get; }
}
