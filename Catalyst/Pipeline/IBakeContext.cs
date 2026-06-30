namespace Catalyst.Pipeline;

using System.Collections.Generic;
using Catalyst.Knowledge;
using Catalyst.Storage;

public interface IBakeContext : IDiagnosticContext {
    IReadOnlyList<IBaker> Bakers { get; }
    List<ResolvedBeing>? Resolved { get; }
    Knowledge? Knowledge { get; set; }
}
