namespace DataCatalyst.Pipeline;

using System.Collections.Generic;
using DataCatalyst.Knowledge;
using DataCatalyst.Storage;

public interface IBakeContext : IDiagnosticContext {
    IReadOnlyList<IBaker> Bakers { get; }
    List<ResolvedBeing>? Resolved { get; }
    Knowledge? Knowledge { get; set; }
}
