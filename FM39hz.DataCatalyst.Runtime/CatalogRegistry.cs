namespace FM39hz.DataCatalyst.Runtime;

using System.Collections.Generic;

public static class CatalogRegistry {
	private static readonly List<System.Type> _catalogs = new();

	public static void Register<T>() {
		lock (_catalogs) {
			if (!_catalogs.Contains(typeof(T))) _catalogs.Add(typeof(T));
		}
	}

	public static System.Type[] GetAll() {
		lock (_catalogs) return _catalogs.ToArray();
	}
}
