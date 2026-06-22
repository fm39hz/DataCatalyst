namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

/// <summary>Schema registry. Core registers built-in fields, plugins register their own.</summary>
public sealed class SchemaBuilder {
	/// <summary>Default instance for backward compatibility and SourceGen registration.</summary>
	public static readonly SchemaBuilder Default = new();

	private readonly Dictionary<string, Type> _fieldTypes = [];
	private readonly Dictionary<string, string> _fieldDefaults = [];

	public SchemaBuilder() {
		Register<string[]>("inherits");
		Register<int>("layer");
	}

	/// <summary>Registers a field name with its C# type. Called by SourceGen at assembly load.</summary>
	public void Register<T>(string fieldName) where T : notnull {
		_fieldTypes[fieldName] = typeof(T);
	}

	/// <summary>Registers a field with runtime Type. For SourceGen.</summary>
	public void Register(string fieldName, Type type) {
		_fieldTypes[fieldName] = type;
	}

	/// <summary>Returns the C# type for a field name. Null if unknown.</summary>
	public Type? ResolveType(string fieldName) =>
		_fieldTypes.TryGetValue(fieldName, out var t) ? t : null;

	/// <summary>Returns true if the field is registered.</summary>
	public bool IsRegistered(string fieldName) => _fieldTypes.ContainsKey(fieldName);
}
