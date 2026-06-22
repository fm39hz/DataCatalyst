namespace DataCatalyst.Abstractions;

using System;
using System.Collections.Generic;

/// <summary>Data entry with typed components and Type-keyed field access.</summary>
public sealed class DataEntry {
	public string Key { get; }
	public IReadOnlyDictionary<Type, object> Components { get; }
	public IReadOnlyDictionary<Type, object> Fields { get; }
	public string? SourceFile { get; set; }

	public DataEntry(string key,
		Dictionary<Type, object>? components = null,
		Dictionary<Type, object>? fields = null) {

		Key = key;
		Components = components is not null
			? new Dictionary<Type, object>(components)
			: new Dictionary<Type, object>();
		Fields = fields is not null
			? new Dictionary<Type, object>(fields)
			: new Dictionary<Type, object>();
	}

	/// <summary>Gets a field by its marker type. T can be string[], int, or a plugin marker type.</summary>
	public T GetField<T>() where T : notnull => (T)Fields[typeof(T)];

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
