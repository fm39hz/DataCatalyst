namespace DataCatalyst.Plugins.ConceptDomain;

using System;
using DataCatalyst.Core;

/// <summary>
/// Extension methods for DataCatalog to access concept-scoped catalogs.
/// </summary>
public static class DataCatalogConceptExtensions {
	/// <summary>
	/// Gets a concept-scoped catalog by tag type.
	/// Requires ConceptDomainPlugin to be registered and configured.
	/// </summary>
	public static ConceptCatalog<TConcept> GetConcept<TConcept>(this DataCatalog catalog)
		where TConcept : struct {

		var plugin = ServiceRegistry.Default.Get<ConceptDomainPlugin>() ?? throw new InvalidOperationException(
				"ConceptDomainPlugin not registered. " +
				"Ensure the plugin is loaded via [DataPlugin] assembly attribute.");
		return plugin.GetConcept<TConcept>();
	}
}
