namespace FM39hz.DataCatalyst.Runtime;

using System;
using System.Collections.Generic;

public static class SchemaMigrationRegistry {
	private static readonly Dictionary<(System.Type type, int major), object> _migrations = new();
	private static readonly object _lock = new();

	public static void Register<T>(int fromMajorVersion, Func<string, Dictionary<string, object>, Dictionary<string, object>> migrator) {
		lock (_lock) { _migrations[(typeof(T), fromMajorVersion)] = migrator; }
	}

	internal static Func<string, Dictionary<string, object>, Dictionary<string, object>>? Get<T>(int fromMajor) {
		lock (_lock) {
			return _migrations.TryGetValue((typeof(T), fromMajor), out var m) ? (Func<string, Dictionary<string, object>, Dictionary<string, object>>)m : null;
		}
	}
}
