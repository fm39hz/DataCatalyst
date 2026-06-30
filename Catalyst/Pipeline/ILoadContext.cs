namespace Catalyst.Pipeline;

using System.Collections.Generic;
using Catalyst.Loader;
using Catalyst.Storage;

public interface ILoadContext : IDiagnosticContext {
    IReadOnlyList<DataSource> Sources { get; }
    HashSet<string> Keys { get; }
    Dictionary<string, List<string>> Mappings { get; }
    List<RawBeing>? Raw { get; set; }
}
