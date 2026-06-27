namespace DataCatalyst.Registry;

using System;
using System.Collections.Generic;

public static class AspectFieldRegistry {
	private static readonly Dictionary<string, Dictionary<string, Type>> _fields = new(StringComparer.OrdinalIgnoreCase);
	private static bool _frozen;

	public static void Register(string aspectName, Dictionary<string, Type> fields) {
		if (_frozen) {
			throw new InvalidOperationException("Registry frozen");
		}

		_fields[aspectName] = fields;
	}

	public static Dictionary<string, Type>? GetFields(string aspectName)
		=> _fields.TryGetValue(aspectName, out var f) ? f : null;

	public static bool HasFields(string aspectName) => _fields.ContainsKey(aspectName);

	internal static void Freeze() => _frozen = true;
}
