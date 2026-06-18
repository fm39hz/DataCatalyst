namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

/// <summary>Registry of known data component types and their JSON discriminators.</summary>
public static class PrimitiveRegistry {
	private static readonly HashSet<Type> _types = [];
	private static readonly Dictionary<string, Type> _ids = [];

	/// <summary>Registers a type. The short type name is used as JSON discriminator.</summary>
	public static void Register<T>() {
		lock (_types) {
			_types.Add(typeof(T));
			_ids[typeof(T).Name] = typeof(T);
		}
	}

	/// <summary>Registers a batch of compile-time generated discriminator mappings.</summary>
	public static void RegisterIds(Dictionary<string, Type> ids) {
		lock (_ids) {
			foreach (var (k, v) in ids) {
				_ids[k] = v;
			}
		}
	}

	/// <summary>Resolves a JSON discriminator to a component type.</summary>
	public static bool TryResolveId(string id, out Type? type) {
		lock (_ids) {
			return _ids.TryGetValue(id, out type);
		}
	}

	/// <summary>Checks if a type is a registered primitive.</summary>
	public static bool IsRegistered(Type type) {
		lock (_types) {
			return _types.Contains(type);
		}
	}

	/// <summary>Returns all registered primitive types.</summary>
	public static IReadOnlyCollection<Type> GetAll() {
		lock (_types) {
			return [.. _types];
		}
	}

	/// <summary>Removes all registered primitives.</summary>
	public static void Clear() {
		lock (_types) {
			_types.Clear();
		}
		lock (_ids) {
			_ids.Clear();
		}
	}
}
