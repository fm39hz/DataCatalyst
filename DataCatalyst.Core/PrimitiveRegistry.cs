namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

/// <summary>Registry of known data component types and their JSON discriminators.</summary>
public class PrimitiveRegistry {
	/// <summary>Default instance for backward compatibility.</summary>
	public static readonly PrimitiveRegistry Default = new();

	private readonly HashSet<Type> _types = [];
	private readonly Dictionary<string, Type> _ids = [];

	/// <summary>Registers a type. The short type name is used as JSON discriminator.</summary>
	public void Register<T>() {
		_types.Add(typeof(T));
		_ids[typeof(T).Name] = typeof(T);
	}

	/// <summary>Registers a batch of compile-time generated discriminator mappings.</summary>
	public void RegisterIds(IReadOnlyDictionary<string, Type> ids) {
		foreach (var (k, v) in ids) {
			_ids[k] = v;
		}
	}

	/// <summary>Resolves a JSON discriminator to a component type.</summary>
	public bool TryResolveId(string id, out Type? type) => _ids.TryGetValue(id, out type);

	/// <summary>Resolves a JSON discriminator to a component type, or null if not found.</summary>
	public Type? ResolveId(string id) => _ids.TryGetValue(id, out var type) ? type : null;

	/// <summary>Checks if a type is a registered primitive.</summary>
	public bool IsRegistered(Type type) => _types.Contains(type);

	/// <summary>Returns all registered primitive types.</summary>
	public IReadOnlyCollection<Type> GetAll() => [.. _types];

	/// <summary>Removes all registered primitives.</summary>
	public void Clear() {
		_types.Clear();
		_ids.Clear();
	}
}
