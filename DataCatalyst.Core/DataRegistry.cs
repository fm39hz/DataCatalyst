namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;
using DataCatalyst.Abstractions;

/// <summary>Instance-based registry for components and plugins.</summary>
public sealed class DataRegistry {
	private readonly HashSet<Type> _components = [];
	private readonly List<IDataPlugin> _plugins = [];

	/// <summary>Registers a component type.</summary>
	public void RegisterComponent<T>() where T : struct {
		lock (_components) {
			_components.Add(typeof(T));
		}
	}

	/// <summary>Registers a component type by Type object.</summary>
	public void RegisterComponent(Type type) {
#if NET6_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(type);
#else
		if (type == null) {
			throw new ArgumentNullException(nameof(type));
		}
#endif
		if (!type.IsValueType) {
			throw new ArgumentException("Component must be a value type (struct).", nameof(type));
		}
		lock (_components) {
			_components.Add(type);
		}
	}

	/// <summary>Registers and instantiates a plugin type.</summary>
	public void RegisterPlugin<T>() where T : IDataPlugin, new() {
		lock (_plugins) {
			_plugins.Add(new T());
		}
	}

	/// <summary>Registers a plugin instance.</summary>
	public void RegisterPlugin(IDataPlugin plugin) {
#if NET6_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(plugin);
#else
		if (plugin == null) {
			throw new ArgumentNullException(nameof(plugin));
		}
#endif
		lock (_plugins) {
			_plugins.Add(plugin);
		}
	}

	/// <summary>Gets all registered component types.</summary>
	public IReadOnlyCollection<Type> GetComponents() {
		lock (_components) {
			return [.. _components];
		}
	}

	/// <summary>Gets all registered plugin instances.</summary>
	public IReadOnlyList<IDataPlugin> GetPlugins() {
		lock (_plugins) {
			return [.. _plugins];
		}
	}
}
