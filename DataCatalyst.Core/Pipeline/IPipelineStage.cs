namespace DataCatalyst.Pipeline;

using System;
using System.Collections.Generic;
using DataCatalyst.Knowledge;
using DataCatalyst.Loader;
using DataCatalyst.Registry;
using DataCatalyst.Schema;
using DataCatalyst.Storage;

public interface IPipelineStage {
	public int Order { get; }
	public bool Execute(PipelineContext ctx);
}

public sealed class PipelineContext(SchemaRegistry schema, IReadOnlyList<DataSource> sources,
	IReadOnlyList<string> ontologyPaths, IReadOnlyList<IBaker> bakers, RegistrySet registries)
	: ILoadContext, IResolveContext, IBakeContext, IOntologyContext {
	public DiagnosticBag Diagnostics { get; } = new();
	public SchemaRegistry Schema { get; } = schema;
	public IReadOnlyList<DataSource> Sources { get; } = sources;
	public IReadOnlyList<string> OntologyPaths { get; } = ontologyPaths;
	public IReadOnlyList<IBaker> Bakers { get; } = bakers;
	public RegistrySet Registries { get; } = registries;
	public List<RawBeing>? Raw { get; set; }
	public List<ResolvedBeing>? Resolved { get; set; }
	public HashSet<string> Keys { get; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, List<string>> Mappings { get; } = new(StringComparer.OrdinalIgnoreCase);
	public Knowledge? Knowledge { get; set; }
}
