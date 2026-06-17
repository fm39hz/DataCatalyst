namespace DataCatalyst.Core;

using System.Collections.Generic;
using Abstractions;

/// <summary>Registry for data view adapters.</summary>
public static class DataViewAdapterRegistry {
	private static readonly RegistryStore<System.Type, object> _adapters = new();

	/// <summary>Registers a view adapter for a component type.</summary>
	public static void Register<T>(IDataViewAdapter<T> adapter) {
		var t = typeof(T);
		if (_adapters.TryGet(t, out var existing) && existing is List<IDataViewAdapter<T>> list) {
			list.Add(adapter);
		}
		else {
			_adapters.Add(t, new List<IDataViewAdapter<T>> { adapter });
		}
	}

	/// <summary>Returns all adapters for a component type.</summary>
	public static IEnumerable<IDataViewAdapter<T>> GetAdapters<T>() {
		if (_adapters.TryGet(typeof(T), out var existing) && existing is List<IDataViewAdapter<T>> list) {
			return [.. list];
		}

		return [];
	}

	/// <summary>Clears all registered adapters.</summary>
	public static void Clear() => _adapters.Clear();
}
