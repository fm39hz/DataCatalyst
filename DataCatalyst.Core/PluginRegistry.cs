namespace DataCatalyst.Core;

using System.Collections.Generic;
using Abstractions;

/// <summary>Registry for plugin discovery and instantiation.</summary>
public class PluginRegistry {
	/// <summary>Default instance for backward compatibility.</summary>
	public static readonly PluginRegistry Default = new();

	private readonly RegistryStore<string, IDataPlugin> _plugins = new();

	/// <summary>Registers and instantiates a plugin type.</summary>
	public void Register<T>() where T : IDataPlugin, new() =>
		_plugins.Add(typeof(T).FullName ?? typeof(T).Name, new T());

	/// <summary>All registered plugin instances.</summary>
	public IReadOnlyList<IDataPlugin> Plugins => _plugins.GetAll();

	/// <summary>Clears all registered plugins.</summary>
	public void Clear() => _plugins.Clear();
}
