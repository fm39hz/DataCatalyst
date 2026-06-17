namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

/// <summary>Data entry with typed components and inheritance.</summary>
public sealed class DataEntry(string key, Dictionary<Type, object>? components = null, List<string>? inherits = null) {
	/// <summary>Unique identifier for this entry.</summary>
	public string Key { get; } = key;
	/// <summary>Parent entry keys to inherit components from.</summary>
	public List<string>? Inherits { get; internal set; } = inherits;
	/// <summary>Component data indexed by type.</summary>
	public IReadOnlyDictionary<Type, object> Components => _components;
	internal Dictionary<Type, object> _components = components ?? [];
	internal bool _resolved;

	/// <summary>The source file path from which this entry was loaded.</summary>
	public string? SourceFile { get; set; }

	/// <summary>Adds or replaces a component value.</summary>
	public void Set<T>(T value) where T : struct => _components[typeof(T)] = value;

	/// <summary>Retrieves a component by type. Throws if missing.</summary>
	public T Get<T>() where T : struct {
		if (_components.TryGetValue(typeof(T), out var boxed)) {
			return (T)boxed;
		}

		throw new KeyNotFoundException($"Component '{typeof(T).Name}' not found in entry '{Key}'");
	}

	/// <summary>Attempts to retrieve a component by type.</summary>
	public bool TryGet<T>(out T value) where T : struct {
		if (_components.TryGetValue(typeof(T), out var boxed)) {
			value = (T)boxed;
			return true;
		}
		value = default;
		return false;
	}

	/// <summary>Checks if a component type exists.</summary>
	public bool Has<T>() where T : struct => _components.ContainsKey(typeof(T));
}
