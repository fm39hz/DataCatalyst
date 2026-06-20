namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions;

/// <summary>Registry for plugin discovery, ordering, and lifecycle management.</summary>
public sealed class PluginRegistry {
	/// <summary>Default instance for backward compatibility.</summary>
	public static readonly PluginRegistry Default = new();

	private readonly Dictionary<string, IPlugin> _plugins = [];

	/// <summary>Registers and instantiates a plugin type (parameterless constructor).</summary>
	public void Register<T>() where T : IPlugin, new() {
		var plugin = new T();
		RegisterInstance(typeof(T).FullName ?? typeof(T).Name, plugin);
	}

	/// <summary>Registers a pre-configured plugin instance (supports DI).</summary>
	public void Register(Type type, IPlugin plugin) {
		RegisterInstance(type.FullName ?? type.Name, plugin);
	}

	/// <summary>Registers a pre-configured plugin instance by explicit key.</summary>
	public void Register(string key, IPlugin plugin) {
		RegisterInstance(key, plugin);
	}

	private void RegisterInstance(string key, IPlugin plugin) {
		_plugins[key] = plugin;
	}

	/// <summary>All registered plugin instances, sorted by execution order.</summary>
	public IReadOnlyList<IPlugin> Plugins {
		get {
			// Sort by Order attribute, then by registration order
			return [.. _plugins.Values.OrderBy(p => GetOrder(p))];
		}
	}

	/// <summary>Enabled plugins in execution order.</summary>
	public IReadOnlyList<IPlugin> EnabledPlugins {
		get {
			return [.. _plugins.Values.Where(p => p.IsEnabled).OrderBy(GetOrder)];
		}
	}

	/// <summary>Initializes all plugins (calls OnLoad).</summary>
	public void LoadAll() {
		foreach (var p in _plugins.Values) {
			p.OnLoad();
		}
	}

	/// <summary>Initializes all registered plugins (calls OnPluginInit).</summary>
	public void InitAll() {
		foreach (var p in _plugins.Values.OfType<IPluginInit>()) {
			p.OnPluginInit();
		}
	}

	/// <summary>Cleans up all registered plugins (calls OnPluginCleanup).</summary>
	public void CleanupAll() {
		foreach (var p in _plugins.Values.OfType<IPluginCleanup>()) {
			p.OnPluginCleanup();
		}
	}

	/// <summary>Clears all registered plugins.</summary>
	public void Clear() => _plugins.Clear();

	/// <summary>Gets a plugin by type.</summary>
	public T? Get<T>() where T : class, IPlugin =>
		_plugins.Values.OfType<T>().FirstOrDefault();

	private static int GetOrder(IPlugin plugin) {
		var attr = plugin.GetType().GetCustomAttributes(typeof(DataPluginAttribute), inherit: true);
		if (attr.Length > 0 && attr[0] is DataPluginAttribute dpa) {
			return dpa.Order;
		}
		return int.MaxValue;
	}
}
