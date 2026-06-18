namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

/// <summary>Registry for consumer-provided mapper implementations consumed by plugins at bake time.</summary>
public static class MapperRegistry {
	private static readonly Dictionary<Type, object> _mappers = [];
	private static readonly object _lock = new();

	/// <summary>Registers a mapper instance by its contract type.</summary>
	public static void Register<T>(T mapper) where T : class {
		lock (_lock) {
			_mappers[typeof(T)] = mapper;
		}
	}

	/// <summary>Retrieves a registered mapper instance.</summary>
	public static T? Get<T>() where T : class {
		lock (_lock) {
			return _mappers.TryGetValue(typeof(T), out var m) ? (T?)m : null;
		}
	}

	/// <summary>Removes all registered mappers.</summary>
	public static void Clear() {
		lock (_lock) {
			_mappers.Clear();
		}
	}
}
