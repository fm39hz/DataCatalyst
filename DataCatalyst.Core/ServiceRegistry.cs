namespace DataCatalyst.Core;

/// <summary>Container for singleton service instances.</summary>
public static class ServiceRegistry {
	private static readonly RegistryStore<System.Type, object> _services = new();

	/// <summary>Registers a service instance.</summary>
	public static void Register<T>(T service) where T : class => _services.Add(typeof(T), service);

	/// <summary>Retrieves a registered service instance.</summary>
	public static T? Get<T>() where T : class {
		_services.TryGet(typeof(T), out var s);
		return (T?)s;
	}

	/// <summary>Clears all registered services.</summary>
	public static void Clear() => _services.Clear();
}
