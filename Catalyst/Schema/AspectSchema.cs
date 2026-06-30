namespace Catalyst.Schema;

using System;
using System.Collections.Generic;

public sealed class AspectSchema {
	public string Name { get; }
	public IReadOnlyDictionary<string, Type> Fields { get; }
	public IReadOnlyDictionary<string, string> FieldTypeNames { get; }

	public AspectSchema(string name, Dictionary<string, Type> fields) {
		Name = name;
		Fields = fields;
		var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var kv in fields) {
			names[kv.Key] = kv.Value.Name;
		}

		FieldTypeNames = names;
	}
}
