namespace Catalyst.Pipeline;

using System.Collections.Generic;
using Catalyst.Registry;
using Catalyst.Schema;
using Catalyst.Storage;

public interface IResolveContext : IDiagnosticContext {
    SchemaRegistry Schema { get; }
    RegistrySet Registries { get; }
    List<RawBeing>? Raw { get; set; }
    List<ResolvedBeing>? Resolved { get; set; }
}
