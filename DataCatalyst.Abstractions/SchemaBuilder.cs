namespace DataCatalyst.Abstractions;

using System;
using System.Collections.Generic;

/// <summary>Maps format-specific field names to C# types for runtime parsing.</summary>
public sealed class SchemaBuilder {
	private readonly Dictionary<string, Type> _schema = [];

	public SchemaBuilder() {
		Register<string[]>("inherits");
		Register<int>("layer");
	}

	/// <summary>Links a format name to its C# type. Called by SourceGen.</summary>
	public void Register<T>(string formatName) where T : notnull {
		_schema[formatName] = typeof(T);
	}

	/// <summary>Links a format name to a marker type. Marker type = field identity.</summary>
	public void Register(string formatName, Type markerType) {
		_schema[formatName] = markerType;
	}

	/// <summary>Returns the marker type for a format name. Null if unknown.</summary>
	public Type? ResolveType(string formatName) =>
		_schema.TryGetValue(formatName, out var t) ? t : null;
}
