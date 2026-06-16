namespace DataCatalyst.Runtime;

using System.Collections.Generic;

public static class ServiceRegistry {
    private static readonly RegistryStore<System.Type, object> _services = new();

    public static void Register<T>(T service) where T : class {
        _services.Add(typeof(T), service);
    }

    public static T? Get<T>() where T : class {
        _services.TryGet(typeof(T), out var s);
        return (T?)s;
    }
}
