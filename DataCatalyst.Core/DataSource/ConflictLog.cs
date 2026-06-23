namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Abstractions;

/// <summary>
/// Ghi nhận lịch sử ghi dữ liệu và cảnh báo xung đột (conflict) giữa các nguồn khác nhau.
/// </summary>
public sealed class ConflictLog {
	private readonly List<string> _diagnostics;
	private readonly Dictionary<(string EntryKey, Type ComponentType), SourceWriteRecord> _writes = new();

	public ConflictLog(List<string> diagnostics) {
		_diagnostics = diagnostics;
	}

	/// <summary>
	/// Ghi nhận nguồn ghi thành công các component cho một entry.
	/// </summary>
	public void RecordWrite(string entryKey, DataSource source, IEnumerable<Type> componentTypes) {
		foreach (var type in componentTypes) {
			_writes[(entryKey, type)] = new SourceWriteRecord(source);
		}
	}

	/// <summary>
	/// Kiểm tra và báo cáo nếu có sự ghi đè trùng lắp giữa các nguồn khác nhau.
	/// </summary>
	public void DetectConflict(string entryKey, Type componentType, DataSource incomingSource) {
		if (_writes.TryGetValue((entryKey, componentType), out var existing)) {
			if (string.Equals(existing.Source.Name, incomingSource.Name, StringComparison.Ordinal)) {
				return; // Cùng một nguồn ghi, không tính là conflict
			}

			// Nguồn Overlay không tranh chấp với main flow
			if (existing.Source.MergePolicy == MergePolicy.Overlay || incomingSource.MergePolicy == MergePolicy.Overlay) {
				return;
			}

			var severity = existing.Source.Priority == incomingSource.Priority ? "Warning" : "Info";
			_diagnostics.Add($"[{severity}] Conflict on '{entryKey}' for component '{componentType.Name}'. " +
				$"Written by '{existing.Source.Name}' (priority {existing.Source.Priority}, policy {existing.Source.MergePolicy}) " +
				$"and overridden by '{incomingSource.Name}' (priority {incomingSource.Priority}, policy {incomingSource.MergePolicy}).");
		}
	}

	/// <summary>
	/// Ghi nhận sự kiện một Entry bị thay thế hoàn toàn (Replace policy).
	/// </summary>
	public void DetectReplacement(string entryKey, DataSource incomingSource) {
		var writers = _writes.Keys
			.Where(k => string.Equals(k.EntryKey, entryKey, StringComparison.Ordinal))
			.Select(k => _writes[k].Source.Name)
			.Distinct()
			.ToList();

		if (writers.Count > 0) {
			_diagnostics.Add($"[Info] Entry '{entryKey}' replaced by source '{incomingSource.Name}' (replacing component authors: {string.Join(", ", writers)}).");
		}
	}

	private sealed class SourceWriteRecord {
		public DataSource Source { get; }
		public SourceWriteRecord(DataSource source) {
			Source = source;
		}
	}
}
