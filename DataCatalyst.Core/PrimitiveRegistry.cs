namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

/// <summary>Registry of known data component types.</summary>
public static class PrimitiveRegistry {
	private static readonly HashSet<Type> _types = [];

	/// <summary>Registers a struct type as a known primitive.</summary>
	public static void Register<T>() where T : struct {
		lock (_types) {
			_types.Add(typeof(T));
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
	}
}
