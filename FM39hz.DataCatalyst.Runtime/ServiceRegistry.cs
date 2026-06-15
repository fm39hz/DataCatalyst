namespace FM39hz.DataCatalyst.Runtime;

using System.Collections.Generic;

public static class ServiceRegistry {
	private static readonly Dictionary<System.Type, object> _services = new();
	private static readonly object _lock = new();

	public static void Register<T>(T service) where T : class {
		lock (_lock) { _services[typeof(T)] = service; }
	}

	public static T? Get<T>() where T : class {
		lock (_lock) {
			return _services.TryGetValue(typeof(T), out var s) ? (T)s : null;
		}
	}
}
