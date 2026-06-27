namespace DataCatalyst.Pipeline;

using System.Collections.Generic;
using DataCatalyst.Registry;
using DataCatalyst.Schema;

public interface IOntologyContext : IDiagnosticContext {
    SchemaRegistry Schema { get; }
    IReadOnlyList<string> OntologyPaths { get; }
    RegistrySet Registries { get; }
}
