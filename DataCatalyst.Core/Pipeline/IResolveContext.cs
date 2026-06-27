namespace DataCatalyst.Pipeline;

using System.Collections.Generic;
using DataCatalyst.Registry;
using DataCatalyst.Schema;
using DataCatalyst.Storage;

public interface IResolveContext : IDiagnosticContext {
    SchemaRegistry Schema { get; }
    RegistrySet Registries { get; }
    List<RawBeing>? Raw { get; set; }
    List<ResolvedBeing>? Resolved { get; set; }
}
