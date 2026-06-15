namespace FM39hz.DataCatalyst.Runtime;

using System.Collections.Generic;
using FM39hz.DataCatalyst.Abstractions;

public static class PluginRegistry {
	private static readonly List<IModPlugin> _plugins = [];

	public static void Register(IModPlugin plugin) {
		_plugins.Add(plugin);
	}

	public static void LoadAll(IModGameContext context) {
		var sorted = TopoSort();
		foreach (var plugin in sorted) {
			plugin.OnLoad(context);
		}
	}

	private static List<IModPlugin> TopoSort() {
		var ordered = new List<IModPlugin>(_plugins.Count);
		var visited = new HashSet<string>();
		var visiting = new HashSet<string>();
		var map = new Dictionary<string, IModPlugin>();
		foreach (var p in _plugins) {
			map[p.Name] = p;
		}
		foreach (var p in _plugins) {
			Visit(p, map, visited, visiting, ordered);
		}
		return ordered;
	}

	private static void Visit(IModPlugin p, Dictionary<string, IModPlugin> map,
		HashSet<string> visited, HashSet<string> visiting, List<IModPlugin> ordered) {
		if (!visited.Add(p.Name)) return;
		visiting.Add(p.Name);
		foreach (var dep in p.Dependencies) {
			if (map.TryGetValue(dep, out var depPlugin)) {
				if (visiting.Contains(dep)) {
					continue;
				}
				Visit(depPlugin, map, visited, visiting, ordered);
			}
		}
		visiting.Remove(p.Name);
		ordered.Add(p);
	}
}
