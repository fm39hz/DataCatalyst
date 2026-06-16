namespace FM39hz.DataCatalyst.DataRoot;

using System;
using System.Collections.Generic;

public static class DataRootPluginRegistry {
    private static readonly List<IDataRootPlugin> _plugins = new();
    private static readonly object _lock = new();

    public static void Register(IDataRootPlugin plugin) {
        lock (_lock) _plugins.Add(plugin);
    }

    public static void Register<T>() where T : IDataRootPlugin, new() {
        lock (_plugins) _plugins.Add(new T());
    }

    public static IReadOnlyList<IDataRootPlugin> GetPlugins() {
        lock (_lock) return _plugins.ToArray();
    }

    public static void Clear() {
        lock (_lock) _plugins.Clear();
    }
}
