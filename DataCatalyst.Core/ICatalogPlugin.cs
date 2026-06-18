namespace DataCatalyst.Core;

using System.Collections.Generic;
using Abstractions;

/// <summary>Plugin that hooks into the resolution pipeline after the catalog is built.</summary>
public interface ICatalogPlugin : IDataPlugin {
	/// <summary>Called after catalog resolution for post-processing or validation.</summary>
	void OnCatalogResolved(DataCatalog catalog, List<string> diagnostics);
}
