namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;
using DataCatalyst.Abstractions;
using DataCatalyst.Core.Utils;

/// <summary>
/// Sắp xếp các nguồn dữ liệu (DataSource) theo thứ tự phụ thuộc (topo-sort) và độ ưu tiên (priority).
/// </summary>
public static class LoadOrderResolver {
	/// <summary>
	/// Sắp xếp danh sách DataSource bằng Kahn's algorithm.
	/// </summary>
	public static List<DataSource> Resolve(IEnumerable<DataSource> sources, List<string>? diagnostics = null) {
		var tieBreaker = Comparer<DataSource>.Create((a, b) => {
			var cmp = a.Priority.CompareTo(b.Priority);
			if (cmp != 0) return cmp;
			return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
		});

		return TopologicalSort.SortKahn(
			sources,
			getKey: s => s.Name,
			getDependencies: s => s.DependsOn,
			tieBreaker: tieBreaker,
			diagnostics: diagnostics
		);
	}
}
