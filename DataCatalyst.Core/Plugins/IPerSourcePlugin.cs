namespace DataCatalyst.Core;

using System.Collections.Generic;
using DataCatalyst.Abstractions;

/// <summary>
/// Hook plugin interface to intercept entries loaded from a specific data source.
/// </summary>
public interface IPerSourcePlugin : IPlugin {
	/// <summary>
	/// Intercept entries right after they are loaded from a DataSource, before deduplication and merging.
	/// </summary>
	void OnSourceLoaded(DataSource source, List<DataEntry> entries, List<string> diagnostics);
}
