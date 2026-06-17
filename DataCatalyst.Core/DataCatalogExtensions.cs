namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

/// <summary>Extension methods for DataCatalog.</summary>
public static class DataCatalogExtensions {
	/// <summary>
	/// Binds components of type TComponent in the catalog to a dictionary keyed by TEnum using the specified kindSelector.
	/// Decoupled from serialization formats, reflection-free, and Native AOT-safe.
	/// </summary>
#if NET8_0_OR_GREATER
	public static FrozenDictionary<TEnum, TComponent> Bind<TEnum, TComponent>(
		this DataCatalog catalog,
		Func<TComponent, TEnum> kindSelector)
		where TEnum : struct, Enum
		where TComponent : struct {
		var dict = new Dictionary<TEnum, TComponent>();

		foreach (var entry in catalog.Entries.Values) {
			if (entry.TryGet<TComponent>(out var comp)) {
				dict[kindSelector(comp)] = comp;
			}
		}

		return dict.ToFrozenDictionary();
	}
#else
	public static IReadOnlyDictionary<TEnum, TComponent> Bind<TEnum, TComponent>(
		this DataCatalog catalog,
		Func<TComponent, TEnum> kindSelector)
		where TEnum : struct, Enum
		where TComponent : struct {

		var dict = new Dictionary<TEnum, TComponent>();

		foreach (var entry in catalog.Entries.Values) {
			if (entry.TryGet<TComponent>(out var comp)) {
				dict[kindSelector(comp)] = comp;
			}
		}

		return dict;
	}
#endif
}
