namespace FM39hz.DataCatalyst.Runtime;

using System.Collections.Generic;
using FM39hz.DataCatalyst.Abstractions;

public static class PluginRegistry {
    private static readonly RegistryStore<string, IDataPlugin> _plugins = new();

    public static void Register<T>() where T : IDataPlugin, new() {
        _plugins.Add(typeof(T).FullName ?? typeof(T).Name, new T());
    }

    public static IReadOnlyList<IDataPlugin> Plugins => _plugins.GetAll();
}
