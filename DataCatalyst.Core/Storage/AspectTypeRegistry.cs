namespace DataCatalyst.Storage;

using System;
using System.Collections.Generic;
using DataCatalyst.Registry;

public sealed class AspectTypeRegistry : IAspectTypeRegistry {
	private readonly Dictionary<string, Type> _typeByName = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<Type, Func<object, object?>> _deserializers = [];

	public bool Frozen { get; private set; }
	public IEnumerable<Type> RegisteredTypes => _typeByName.Values;

	public bool TryGetType(string name, out Type? type)
		=> _typeByName.TryGetValue(name, out type);

	public bool TryGetType(ReadOnlySpan<char> name, out Type? type)
		=> _typeByName.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out type);

	public bool HasType(string name) => _typeByName.ContainsKey(name);

	public void Register(Type type) {
		if (Frozen) throw new InvalidOperationException("Registry frozen after pipeline build");
		_typeByName[type.Name] = type;
	}

	public void RegisterDeserializer(Type type, Func<object, object?> d) {
		if (Frozen) throw new InvalidOperationException("Registry frozen after pipeline build");
		_deserializers[type] = d;
	}

	public void Freeze() {
		Frozen = true;
	}

	public object? Deserialize(Type type, object? raw) {
		if (raw == null) {
			return null;
		}

		return _deserializers.TryGetValue(type, out var d) ? d(raw) : null;
	}
}
