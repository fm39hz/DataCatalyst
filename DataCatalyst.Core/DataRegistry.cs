namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;
using Abstractions;

/// <summary>Instance-based registry for components and plugins.</summary>
public sealed class DataRegistry {
	private readonly HashSet<Type> _components = [];
	private readonly List<IPlugin> _plugins = [];

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
		if (!type.IsValueType || type.IsByRefLike) {
			throw new ArgumentException("Component must be a value type (struct) and cannot be a ref struct.", nameof(type));
		}

		lock (_components) {
			_components.Add(type);
		}
	}

	/// <summary>Registers and instantiates a plugin type.</summary>
	public void RegisterPlugin<T>() where T : IPlugin, new() {
		lock (_plugins) {
			_plugins.Add(new T());
		}
	}

	/// <summary>Registers a plugin instance.</summary>
	public void RegisterPlugin(IPlugin plugin) {
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
	public IReadOnlyList<IPlugin> GetPlugins() {
		lock (_plugins) {
			return [.. _plugins];
		}
	}
}
