namespace DataCatalyst.Loaders;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Registry to combine and resolve source-generated JSON serialization contexts under NativeAOT.
/// </summary>
public static class JsonResolverRegistry {
	private static readonly List<IJsonTypeInfoResolver> _resolvers = new() {
		JsonLoaderSystemTypesContext.Default
	};

	/// <summary>
	/// Registers a source-generated JSON serializer context.
	/// Called automatically by compiler-generated ModuleInitializer.
	/// </summary>
	public static void Register(IJsonTypeInfoResolver resolver) {
		if (resolver == null) throw new ArgumentNullException(nameof(resolver));
		lock (_resolvers) {
			_resolvers.Add(resolver);
		}
	}

	/// <summary>
	/// Combines all registered resolvers into a single type info resolver.
	/// </summary>
	public static IJsonTypeInfoResolver GetCombinedResolver() {
		lock (_resolvers) {
			return JsonTypeInfoResolver.Combine(_resolvers.ToArray());
		}
	}
}
