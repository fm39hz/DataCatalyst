namespace DataCatalyst.Loader;

using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class OntologyEntry(string name, string[] reveals, string[] suggests) {
	public string ConceptName { get; } = name;
	public string[] Reveals { get; } = reveals;
	public string[] Suggests { get; } = suggests;
}

public sealed class LoadResult {
	private readonly List<object> _beings = [];
	private readonly List<string> _diagnostics = [];
	private readonly Dictionary<string, List<string>> _mappings = new(System.StringComparer.OrdinalIgnoreCase);

	public IReadOnlyList<object> Beings => _beings;
	public IReadOnlyList<string> Diagnostics => _diagnostics;
	public IReadOnlyDictionary<string, List<string>> Mappings => _mappings;

	internal void AddBeing(object being) => _beings.Add(being);
	internal void AddBeings(LoadResult other) => _beings.AddRange(other._beings);
	internal void AddDiagnostic(string msg) => _diagnostics.Add(msg);
	internal void AddDiagnostics(LoadResult other) => _diagnostics.AddRange(other._diagnostics);
	internal void AddMappings(LoadResult other) {
		foreach (var kv in other._mappings) {
			AddMappings(kv.Key, kv.Value);
		}
	}

	internal void EnsureMapping(string conceptName) {
		if (!_mappings.ContainsKey(conceptName)) {
			_mappings[conceptName] = [];
		}
	}

	internal void AddMapping(string conceptName, string aspectName) {
		if (!_mappings.TryGetValue(conceptName, out var list)) {
			_mappings[conceptName] = list = [];
		}

		if (!list.Contains(aspectName)) {
			list.Add(aspectName);
		}
	}

	internal void AddMappings(string conceptName, List<string> aspects) {
		if (!_mappings.TryGetValue(conceptName, out var list)) {
			_mappings[conceptName] = list = [];
		}

		foreach (var v in aspects) {
			if (!list.Contains(v)) {
				list.Add(v);
			}
		}
	}
}

	public interface IDataLoader {
	public LoadResult Load(string content, string fallbackKey);
	public LoadResult LoadFile(string path);
	public LoadResult LoadDirectory(string path);

	public LoaderFileType DetectFileType(string content) => LoaderFileType.Unknown;

	public Task<LoadResult> LoadAsync(string content, string fallbackKey)
		=> Task.FromResult(Load(content, fallbackKey));
	public Task<LoadResult> LoadFileAsync(string path)
		=> Task.FromResult(LoadFile(path));
	public Task<LoadResult> LoadDirectoryAsync(string path)
		=> Task.FromResult(LoadDirectory(path));
}
