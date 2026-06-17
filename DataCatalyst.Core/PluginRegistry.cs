namespace DataCatalyst.Core;

using System.Collections.Generic;
using DataCatalyst.Abstractions;

/// <summary>Registry for plugin discovery and instantiation.</summary>
public static class PluginRegistry {
	private static readonly RegistryStore<string, IDataPlugin> _plugins = new();

	/// <summary>Registers and instantiates a plugin type.</summary>
	public static void Register<T>() where T : IDataPlugin, new() => _plugins.Add(typeof(T).FullName ?? typeof(T).Name, new T());

	/// <summary>Registers a plugin by fully qualified type name.</summary>
	public static void RegisterByTypeName(string typeName) {
		var type = System.Type.GetType(typeName);
		if (type == null) {
			return;
		}

		if (System.Activator.CreateInstance(type) is IDataPlugin plugin) {
			_plugins.Add(type.FullName ?? type.Name, plugin);
		}
	}

	/// <summary>All registered plugin instances.</summary>
	public static IReadOnlyList<IDataPlugin> Plugins => _plugins.GetAll();

	/// <summary>Clears all registered plugins.</summary>
	public static void Clear() => _plugins.Clear();
}
