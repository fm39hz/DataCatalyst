namespace DataCatalyst.Core.Utils;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Reusable graph sorting utilities.
/// </summary>
public static class TopologicalSort {
	/// <summary>
	/// Performs Kahn's topological sort on a set of items, resolving dependencies.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <typeparam name="TKey">The key type used for lookup.</typeparam>
	/// <param name="items">The collection of items to sort.</param>
	/// <param name="getKey">Function to extract the key of an item.</param>
	/// <param name="getDependencies">Function to get the keys of dependencies for an item.</param>
	/// <param name="tieBreaker">Optional comparer to sort independent items deterministically.</param>
	/// <param name="diagnostics">Optional list to record cycles or missing dependencies.</param>
	/// <returns>A topologically sorted list of items.</returns>
	public static List<T> SortKahn<T, TKey>(
		IEnumerable<T> items,
		Func<T, TKey> getKey,
		Func<T, IEnumerable<TKey>> getDependencies,
		IComparer<T>? tieBreaker = null,
		List<string>? diagnostics = null) where TKey : notnull {

		var list = items.ToList();
		var map = list.ToDictionary(getKey);
		var indegree = list.ToDictionary(getKey, _ => 0);
		var edges = list.ToDictionary(getKey, _ => new List<TKey>());

		// Build dependency graph
		foreach (var item in list) {
			var key = getKey(item);
			var deps = getDependencies(item);
			if (deps == null) continue;

			foreach (var dep in deps) {
				if (map.ContainsKey(dep)) {
					edges[dep].Add(key);
					indegree[key]++;
				}
				else {
					diagnostics?.Add($"Item '{key}' depends on missing dependency '{dep}'.");
				}
			}
		}

		// Collect ready nodes (indegree == 0)
		var ready = list.Where(item => indegree[getKey(item)] == 0).ToList();
		if (tieBreaker != null) {
			ready.Sort(tieBreaker);
		}

		var result = new List<T>();

		while (ready.Count > 0) {
			var cur = ready[0];
			ready.RemoveAt(0);
			result.Add(cur);

			var curKey = getKey(cur);
			foreach (var nextKey in edges[curKey]) {
				indegree[nextKey]--;
				if (indegree[nextKey] == 0) {
					var nextItem = map[nextKey];
					if (tieBreaker != null) {
						var idx = ready.BinarySearch(nextItem, tieBreaker);
						if (idx < 0) idx = ~idx;
						ready.Insert(idx, nextItem);
					}
					else {
						ready.Add(nextItem);
					}
				}
			}
		}

		// Detect cycles
		if (result.Count < list.Count) {
			var cycleKeys = list.Where(item => indegree[getKey(item)] > 0).Select(getKey);
			diagnostics?.Add($"Cycle detected among dependencies: {string.Join(", ", cycleKeys)}");
			
			// Append remaining items to avoid data loss
			foreach (var item in list) {
				if (!result.Contains(item)) {
					result.Add(item);
				}
			}
		}

		return result;
	}
}
