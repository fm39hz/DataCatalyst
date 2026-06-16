namespace FM39hz.DataCatalyst.Runtime;

using System.Collections.Generic;
using FM39hz.DataCatalyst.Abstractions;

public static class PluginRegistry {
    private static readonly List<IDataPlugin> _plugins = [];

    public static void Register<T>() where T : IDataPlugin, new() {
        _plugins.Add(new T());
    }

    public static IReadOnlyList<IDataPlugin> Plugins => _plugins;
}
