namespace DataCatalyst.Abstractions;

using System.Collections.Generic;

/// <summary>Result of a load operation, containing resolved entries and diagnostics.</summary>
public sealed class LoadResult {
	internal readonly List<DataEntry> _entries = [];
	internal readonly List<string> _diagnostics = [];

	/// <summary>The successfully loaded data entries.</summary>
	public IReadOnlyList<DataEntry> Entries => _entries;

	/// <summary>Warnings, errors, or info messages collected during the load operation.</summary>
	public IReadOnlyList<string> Diagnostics => _diagnostics;
}

/// <summary>Loads data entries from an external source. Format-agnostic contract.</summary>
public interface IDataLoader {
	/// <summary>Loads entries from a single file.</summary>
	LoadResult LoadFile(string path);

	/// <summary>Loads entries from all files in a directory.</summary>
	LoadResult LoadDirectory(string path);
}
