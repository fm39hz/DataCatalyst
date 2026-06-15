namespace FM39hz.DataCatalyst.Runtime;

using FM39hz.DataCatalyst.Abstractions;

public static class DataBackendSelector {
	private static volatile DataBackend _current = 0;
	private static volatile bool _initialized;
	private static readonly object _lock = new();

	public static void Initialize(string? backendOverride = null) {
		var value = backendOverride ?? global::System.Environment.GetEnvironmentVariable("DATACATALYST_BACKEND");
		var parsed = (value?.ToLowerInvariant()) switch {
			"sqlite" => DataBackend.Sqlite,
			"json" => DataBackend.Json,
			"all" => DataBackend.All,
			_ => DataBackend.None,
		};
		lock (_lock) {
			_current = parsed;
			_initialized = true;
		}
	}

	public static DataBackend Current {
		get {
			if (!_initialized) {
				lock (_lock) {
					if (!_initialized) {
						Initialize();
					}
				}
			}
			return _current;
		}
	}
}
