namespace DataCatalyst.Abstractions;

using System;
using System.Collections.Generic;

/// <summary>Describes a single field in a data entry.</summary>
public sealed class FieldSchema {
	public string Name { get; }
	public Type Type { get; }
	public bool IsComponent { get; }

	public FieldSchema(string name, Type type, bool isComponent = false) {
		Name = name;
		Type = type;
		IsComponent = isComponent;
	}
}

/// <summary>Aggregates field schemas from Core, Plugins, and data auto-detection.</summary>
public sealed class SchemaBuilder {
	private readonly Dictionary<string, FieldSchema> _schemas = [];

	public void Register(FieldSchema schema) {
		_schemas[schema.Name] = schema;
	}

	public FieldSchema? Find(string name) {
		return _schemas.TryGetValue(name, out var schema) ? schema : null;
	}

	/// <summary>Auto-registers a component type from JSON data discovery.</summary>
	public void RegisterComponent(string name, Type type) {
		if (!_schemas.ContainsKey(name)) {
			_schemas[name] = new FieldSchema(name, type, isComponent: true);
		}
	}

	/// <summary>Gets all registered schemas.</summary>
	public IEnumerable<FieldSchema> All => _schemas.Values;
}
