namespace DataCatalyst.Loader;

using System.Collections.Generic;

public sealed class LoadResult {
	internal List<object> _entries = [];
	internal List<string> _diagnostics = [];
	internal Dictionary<string, List<string>> _mappings = new(System.StringComparer.OrdinalIgnoreCase);
	public IReadOnlyList<object> Entries => _entries;
	public IReadOnlyList<string> Diagnostics => _diagnostics;
	public IReadOnlyDictionary<string, List<string>> Mappings => _mappings;
}

public interface IDataLoader {
	public LoadResult Load(string content, string fallbackKey);
	public LoadResult LoadFile(string path);
	public LoadResult LoadDirectory(string path);
}
