namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

/// <summary>Extension methods for DataCatalog.</summary>
public static class DataCatalogExtensions {
	/// <summary>
	/// Binds components of type TComponent in the catalog to a dictionary keyed by TKey using the specified keySelector.
	/// Decoupled from serialization formats, reflection-free, and Native AOT-safe.
	/// </summary>
	public static IReadOnlyDictionary<TKey, TComponent> Bind<TKey, TComponent>(
		this DataCatalog catalog,
		Func<TComponent, TKey> keySelector)
		where TKey : notnull
		where TComponent : struct {

		var dict = new Dictionary<TKey, TComponent>();

		foreach (var entry in catalog.Entries.Values) {
			if (entry.TryGet<TComponent>(out var comp)) {
				dict[keySelector(comp)] = comp;
			}
		}

#if NET8_0_OR_GREATER
		return dict.ToFrozenDictionary();
#else
		return dict;
#endif
	}
}
