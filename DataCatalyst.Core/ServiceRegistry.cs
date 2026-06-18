namespace DataCatalyst.Core;

using System;

/// <summary>Container for singleton service instances.</summary>
public class ServiceRegistry {
	/// <summary>Default instance for backward compatibility.</summary>
	public static readonly ServiceRegistry Default = new();

	private readonly RegistryStore<Type, object> _services = new();

	/// <summary>Registers a service instance.</summary>
	public void Register<T>(T service) where T : class => _services.Add(typeof(T), service);

	/// <summary>Retrieves a registered service instance.</summary>
	public T? Get<T>() where T : class {
		_services.TryGet(typeof(T), out var s);
		return (T?)s;
	}

	/// <summary>Clears all registered services.</summary>
	public void Clear() => _services.Clear();
}
