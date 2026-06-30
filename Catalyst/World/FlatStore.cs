namespace Catalyst.Knowledge;

using System;
using System.Collections.Generic;

public sealed class FlatStore {
	internal readonly Dictionary<Type, Array> _flats = [];

	internal T[] Get<T>() where T : struct
		=> (T[])_flats[typeof(T)];

	internal bool TryGet<T>(out T[] result) where T : struct {
		if (_flats.TryGetValue(typeof(T), out var arr)) {
			result = (T[])arr;
			return true;
		}
		result = [];
		return false;
	}

	public void Set<T>(T[] array) where T : struct
		=> _flats[typeof(T)] = array;
}
