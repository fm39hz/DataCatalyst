namespace DataCatalyst.Registry;

using System;
using System.Collections.Generic;

public sealed class AspectFieldRegistry : IAspectFieldRegistry {
	private readonly Dictionary<string, Dictionary<string, Type>> _fields = new(StringComparer.OrdinalIgnoreCase);

	public bool Frozen { get; private set; }

	public void Register(string aspectName, Dictionary<string, Type> fields) {
		if (Frozen) {
			throw new InvalidOperationException("Registry frozen");
		}

		_fields[aspectName] = fields;
	}

	public Dictionary<string, Type>? GetFields(string aspectName)
		=> _fields.TryGetValue(aspectName, out var f) ? new Dictionary<string, Type>(f) : null;

	public bool HasFields(string aspectName) => _fields.ContainsKey(aspectName);

	public void Freeze() => Frozen = true;
}
