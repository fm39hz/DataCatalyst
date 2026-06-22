namespace DataCatalyst.Core;

using System.Collections.Generic;
using DataCatalyst.Abstractions;

/// <summary>Fluent API for composing multiple data sources before loading.</summary>
public class DataPipeline {
	private readonly DataCatalystEnvironment _env;
	private readonly List<DataEntry> _entries = [];
	private readonly List<string> _diagnostics = [];

	public DataPipeline(DataCatalystEnvironment? env = null) {
		_env = env ?? new DataCatalystEnvironment();
		_env.Plugins.Register<GameConceptPlugin>();
	}

	/// <summary>Loads a directory using the specified loader. Chainable.</summary>
	public DataPipeline Load(IDataLoader loader, string path) {
		var result = loader.LoadDirectory(path);
		_entries.AddRange(result.Entries);
		_diagnostics.AddRange(result.Diagnostics);
		return this;
	}

	/// <summary>Builds the catalog from all loaded sources.</summary>
	public DataCatalog Build() {
		var graph = DataGraphBuilder.Build(_entries, _diagnostics, _env);
		return DataCatalogBuilder.Resolve(graph, _diagnostics, _env);
	}

	/// <summary>Diagnostics collected from all loaders.</summary>
	public IReadOnlyList<string> Diagnostics => _diagnostics;
}
