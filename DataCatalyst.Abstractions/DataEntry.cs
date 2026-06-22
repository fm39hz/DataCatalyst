namespace DataCatalyst.Abstractions;

using System;
using System.Collections.Generic;

/// <summary>Data entry with typed components only. Everything is a component.</summary>
public sealed class DataEntry {
	public string Key { get; }
	public IReadOnlyDictionary<Type, object> Components { get; }
	public string? SourceFile { get; set; }

	public DataEntry(string key, Dictionary<Type, object>? components = null) {
		Key = key;
		Components = components is not null
			? new Dictionary<Type, object>(components)
			: new Dictionary<Type, object>();
	}

	public T Get<T>() where T : struct {
		if (Components.TryGetValue(typeof(T), out var boxed)) return (T)boxed;
		throw new KeyNotFoundException($"Component '{typeof(T).Name}' not found in entry '{Key}'");
	}

	public bool TryGet<T>(out T value) where T : struct {
		if (Components.TryGetValue(typeof(T), out var boxed)) {
			value = (T)boxed;
			return true;
		}
		value = default;
		return false;
	}

	public bool Has<T>() where T : struct => Components.ContainsKey(typeof(T));
}
