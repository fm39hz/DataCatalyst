using System.Collections.Generic;

namespace DataCatalyst.Loader;

public sealed class LoadResult
{
    internal List<object> _entries = new();
    internal List<string> _diagnostics = new();
    public IReadOnlyList<object> Entries => _entries;
    public IReadOnlyList<string> Diagnostics => _diagnostics;
}

public interface IDataLoader
{
    LoadResult Load(string content, string fallbackKey);
    LoadResult LoadFile(string path);
    LoadResult LoadDirectory(string path);
}
