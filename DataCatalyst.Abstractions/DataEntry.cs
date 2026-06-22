namespace DataCatalyst.Abstractions;

using System;
using System.Collections.Generic;

/// <summary>Data entry with typed components and meta data.</summary>
public sealed class DataEntry {
	/// <summary>Unique identifier for this entry.</summary>
	public string Key { get; }

	/// <summary>Component data indexed by type.</summary>
	public IReadOnlyDictionary<Type, object> Components { get; }

	/// <summary>Reserved meta fields (inherits, Concept, Layer, etc.).</summary>
	public IReadOnlyDictionary<string, object> Meta { get; }

	/// <summary>The source file path from which this entry was loaded.</summary>
	public string? SourceFile { get; set; }

	public readonly Dictionary<Type, object> MutableComponents;

	public DataEntry(string key,
		Dictionary<Type, object>? components = null,
		Dictionary<string, object>? meta = null) {

		Key = key;
		MutableComponents = components ?? [];
		Components = MutableComponents;
		Meta = meta is not null
			? new Dictionary<string, object>(meta)
			: new Dictionary<string, object>();
	}

	/// <summary>Retrieves a component by type. Throws if missing.</summary>
	public T Get<T>() where T : struct {
		if (Components.TryGetValue(typeof(T), out var boxed)) {
			return (T)boxed;
		}
		throw new KeyNotFoundException($"Component '{typeof(T).Name}' not found in entry '{Key}'");
	}

	/// <summary>Attempts to retrieve a component by type.</summary>
	public bool TryGet<T>(out T value) where T : struct {
		if (Components.TryGetValue(typeof(T), out var boxed)) {
			value = (T)boxed;
			return true;
		}
		value = default;
		return false;
	}

	/// <summary>Checks if a component type exists.</summary>
	public bool Has<T>() where T : struct => Components.ContainsKey(typeof(T));
}
