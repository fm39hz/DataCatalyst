namespace Catalyst.Pipeline;

using System.Collections.Generic;
using Catalyst.Knowledge;
using Catalyst.Storage;

public interface IBakeContext : IDiagnosticContext {
	public List<ResolvedBeing>? Resolved { get; }
	public Knowledge? Knowledge { get; set; }
}
