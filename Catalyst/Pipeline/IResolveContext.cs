namespace Catalyst.Pipeline;

using System.Collections.Generic;
using Catalyst.Registry;
using Catalyst.Schema;
using Catalyst.Storage;

public interface IResolveContext : IDiagnosticContext {
	public SchemaRegistry Schema { get; }
	public RegistrySet Registries { get; }
	public List<RawBeing>? Raw { get; set; }
	public List<ResolvedBeing>? Resolved { get; set; }
}
