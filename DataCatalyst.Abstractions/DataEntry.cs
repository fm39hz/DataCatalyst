namespace DataCatalyst.Abstractions;

using System;
using System.Collections.Generic;

public sealed class DataEntry {
	public string Key { get; }
	public IReadOnlyDictionary<Type, object> Components { get; }
	public IReadOnlyDictionary<Type, object> Fields { get; }
	public string? SourceFile { get; set; }

	public DataEntry(string key,
		Dictionary<Type, object>? components = null,
		Dictionary<Type, object>? fields = null) {
		Key = key;
		Components = components ?? [];
		Fields = fields ?? [];
	}

	public T GetField<T>() where T : notnull {
		if (Fields.TryGetValue(typeof(T), out var v)) return (T)v;
		throw new KeyNotFoundException($"Field '{typeof(T).Name}' not found in entry '{Key}'");
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
