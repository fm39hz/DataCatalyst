namespace Catalyst.Pipeline;

using System.Collections.Generic;
using Catalyst.Loader;
using Catalyst.Storage;

public interface ILoadContext : IDiagnosticContext {
	public IReadOnlyList<DataSource> Sources { get; }
	public HashSet<string> Keys { get; }
	public Dictionary<string, List<string>> Mappings { get; }
	public List<RawBeing>? Raw { get; set; }
}
