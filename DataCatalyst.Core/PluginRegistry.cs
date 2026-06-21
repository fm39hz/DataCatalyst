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
	private readonly Dictionary<Type, Type[]?> _dependsOn = [];
	private readonly List<string> _diags = [];

	/// <summary>Registers and instantiates a plugin type (parameterless constructor).</summary>
	public void Register<T>() where T : IPlugin, new() {
		var plugin = new T();
		CacheDependsOn<T>();
		RegisterInstance(typeof(T).FullName ?? typeof(T).Name, plugin);
	}

	/// <summary>Registers a pre-configured plugin instance (supports DI).</summary>
	public void Register(Type type, IPlugin plugin) {
#if NET6_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(type);
		ArgumentNullException.ThrowIfNull(plugin);
#else
		if (type == null) throw new ArgumentNullException(nameof(type));
		if (plugin == null) throw new ArgumentNullException(nameof(plugin));
#endif
		CacheDependsOn(type);
		RegisterInstance(type.FullName ?? type.Name, plugin);
	}

	/// <summary>Registers a pre-configured plugin instance by explicit key.</summary>
	public void Register(string key, IPlugin plugin) {
#if NET6_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(key);
		ArgumentNullException.ThrowIfNull(plugin);
#else
		if (key == null) throw new ArgumentNullException(nameof(key));
		if (plugin == null) throw new ArgumentNullException(nameof(plugin));
#endif
		CacheDependsOn(plugin.GetType());
		RegisterInstance(key, plugin);
	}

	private void RegisterInstance(string key, IPlugin plugin) {
		_plugins[key] = plugin;
	}

	private void CacheDependsOn<T>() where T : IPlugin {
		var attr = Attribute.GetCustomAttribute(typeof(T), typeof(DataPluginAttribute)) as DataPluginAttribute;
		_dependsOn[typeof(T)] = attr?.DependsOn;
	}

	private void CacheDependsOn(Type type) {
		var attr = Attribute.GetCustomAttribute(type, typeof(DataPluginAttribute)) as DataPluginAttribute;
		_dependsOn[type] = attr?.DependsOn;
	}

	/// <summary>All registered plugin instances, sorted by topological dependencies.</summary>
	public IReadOnlyList<IPlugin> Plugins => TopoSort(_plugins.Values);

	/// <summary>Enabled plugins in topological order.</summary>
	public IReadOnlyList<IPlugin> EnabledPlugins =>
		TopoSort(_plugins.Values.Where(p => p.IsEnabled));

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
	public void Clear() {
		_plugins.Clear();
		_diags.Clear();
	}

	/// <summary>Diagnostic messages collected during plugin resolution (e.g. missing dependencies).</summary>
	public IReadOnlyList<string> Diagnostics => _diags;

	/// <summary>Gets a plugin by type.</summary>
	public T? Get<T>() where T : class, IPlugin =>
		_plugins.Values.OfType<T>().FirstOrDefault();

	private List<IPlugin> TopoSort(IEnumerable<IPlugin> plugins) {
		var list = plugins.ToList();
		var map = new Dictionary<Type, IPlugin>();
		var indeg = new Dictionary<Type, int>();
		var edges = new Dictionary<Type, List<Type>>();

		foreach (var p in list) {
			map[p.GetType()] = p;
			indeg[p.GetType()] = 0;
			edges[p.GetType()] = [];
		}

		foreach (var p in list) {
			var deps = GetDependsOn(p.GetType());
			if (deps == null) continue;
			foreach (var dep in deps) {
				if (map.ContainsKey(dep)) {
					edges[dep].Add(p.GetType());
					indeg[p.GetType()]++;
				}
				else {
					_diags.Add($"Plugin '{p.GetType().Name}' depends on '{dep.Name}' which is not registered. Order may be incorrect.");
				}
			}
		}

		var ready = new List<Type>(indeg.Where(kv => kv.Value == 0).Select(kv => kv.Key));
		ready.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));
		var result = new List<IPlugin>();

		while (ready.Count > 0) {
			var cur = ready[0];
			ready.RemoveAt(0);
			result.Add(map[cur]);
			foreach (var next in edges[cur]) {
				if (--indeg[next] == 0) {
					ready.Add(next);
					ready.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));
				}
			}
		}

		return result;
	}

	private Type[]? GetDependsOn(Type type) =>
		_dependsOn.TryGetValue(type, out var deps) ? deps : null;
}
