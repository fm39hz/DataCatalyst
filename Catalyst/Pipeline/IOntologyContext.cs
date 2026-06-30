namespace Catalyst.Pipeline;

using System.Collections.Generic;
using Catalyst.Registry;
using Catalyst.Schema;

public interface IOntologyContext : IDiagnosticContext {
    SchemaRegistry Schema { get; }
    IReadOnlyList<string> OntologyPaths { get; }
    RegistrySet Registries { get; }
}
