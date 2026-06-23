namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

/// <summary>Registry for consumer-provided mapper implementations consumed by plugins at bake time.</summary>
public class MapperRegistry {
	/// <summary>Default instance for backward compatibility.</summary>
	public static readonly MapperRegistry Default = new();

	private readonly Dictionary<Type, object> _mappers = [];

	/// <summary>Registers a mapper instance by its contract type.</summary>
	public void Register<T>(T mapper) where T : class => _mappers[typeof(T)] = mapper;

	/// <summary>Retrieves a registered mapper instance.</summary>
	public T? Get<T>() where T : class =>
		_mappers.TryGetValue(typeof(T), out var m) ? (T?)m : null;

	/// <summary>Removes all registered mappers.</summary>
	public void Clear() => _mappers.Clear();
}
