namespace FM39hz.DataCatalyst.Runtime;

using System.Collections.Generic;
using FM39hz.DataCatalyst.Abstractions;

public static class DataViewAdapterRegistry {
	private static readonly Dictionary<System.Type, object> _adapters = new();
	private static readonly object _lock = new();

	public static void Register<T>(IDataViewAdapter<T> adapter) {
		lock (_lock) {
			var t = typeof(T);
			if (_adapters.TryGetValue(t, out var existing)) {
				var list = (List<IDataViewAdapter<T>>)existing;
				list.Add(adapter);
			} else {
				_adapters[t] = new List<IDataViewAdapter<T>> { adapter };
			}
		}
	}

	public static IEnumerable<IDataViewAdapter<T>> GetAdapters<T>() {
		lock (_lock) {
			if (_adapters.TryGetValue(typeof(T), out var existing)) {
				var list = (List<IDataViewAdapter<T>>)existing;
				return list.ToArray();
			}
		}
		return [];
	}
}
