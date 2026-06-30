namespace Catalyst.Storage;

using System;
using System.Collections.Generic;
using Catalyst.Registry;

public interface IAspectTypeRegistry : IFreezable {
	public IEnumerable<Type> RegisteredTypes { get; }
	public bool TryGetType(string name, out Type? type);
	public bool TryGetType(ReadOnlySpan<char> name, out Type? type);
	public bool HasType(string name);
	public void Register(Type type);
	public void RegisterDeserializer(Type type, Func<object, object?> d);
	public object? Deserialize(Type type, object? raw);
}
