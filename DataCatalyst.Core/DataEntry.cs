namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

/// <summary>Data entry with typed components and inheritance.</summary>
public sealed class DataEntry(string key, Dictionary<Type, object>? components = null, List<string>? inherits = null) {
	/// <summary>Unique identifier for this entry.</summary>
	public string Key { get; } = key;

	/// <summary>Parent entry keys to inherit components from.</summary>
	public List<string>? Inherits { get; internal set; } = inherits;

	/// <summary>Component data indexed by type. Frozen after construction.</summary>
	public IReadOnlyDictionary<Type, object> Components { get; } = components is null
		? new Dictionary<Type, object>()
#if NET8_0_OR_GREATER
		: components.ToFrozenDictionary();
#else
		: components;
#endif

	/// <summary>The source file path from which this entry was loaded.</summary>
	public string? SourceFile { get; init; }

	/// <summary>Patch layer priority. Higher layers override lower layers during merge.</summary>
	public int Layer { get; init; }

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
