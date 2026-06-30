namespace DataCatalyst.Loaders;

#pragma warning disable RS1035
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DataCatalyst.Loader;
using DataCatalyst.Registry;
using DataCatalyst.Storage;

public sealed class JsonDataLoader(IBeingRegistry? registry = null) : IDataLoader {
	private readonly IBeingRegistry? _registry = registry;

	public LoaderFileType DetectFileType(string content) {
		try {
			using var d = JsonDocument.Parse(content);
			var root = d.RootElement;
			if (root.ValueKind != JsonValueKind.Object) return LoaderFileType.Unknown;

			if (root.TryGetProperty("concepts", out var c) && c.ValueKind == JsonValueKind.Object)
				return LoaderFileType.Concept;
			if (root.TryGetProperty("aspects", out var a) && a.ValueKind == JsonValueKind.Object)
				return LoaderFileType.Aspect;

			foreach (var entry in root.EnumerateObject()) {
				if (entry.Name.StartsWith('$') || entry.Value.ValueKind != JsonValueKind.Object)
					continue;
				foreach (var sub in entry.Value.EnumerateObject()) {
					if (sub.Name.StartsWith('$'))
						return LoaderFileType.Being;
				}
			}
			return LoaderFileType.Unknown;
		}
		catch (JsonException) {
			return LoaderFileType.Unknown;
		}
	}

	public LoadResult Load(string content, string fallbackKey) {
		var result = new LoadResult();
		try { ParseJson(content, fallbackKey, result); }
		catch (JsonException ex) { result.AddDiagnostic($"Error parsing JSON for '{fallbackKey}': {ex.Message}"); }
		catch (IOException ex) { result.AddDiagnostic($"IO error for '{fallbackKey}': {ex.Message}"); }
		return result;
	}

	public LoadResult LoadFile(string path) {
		try { return Load(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path)); }
		catch (JsonException ex) { var r = new LoadResult(); r.AddDiagnostic($"Error parsing '{path}': {ex.Message}"); return r; }
		catch (IOException ex) { var r = new LoadResult(); r.AddDiagnostic($"Error loading '{path}': {ex.Message}"); return r; }
	}

	public LoadResult LoadDirectory(string path) {
		var result = new LoadResult();
		if (!Directory.Exists(path)) { result.AddDiagnostic($"Directory not found: {path}"); return result; }
		LoadDirRecursive(path, result);
		return result;
	}

	private void LoadDirRecursive(string dir, LoadResult result) {
		foreach (var f in Directory.EnumerateFiles(dir, "*.json")) {
			var fr = LoadFile(f);
			result.AddBeings(fr);
			result.AddDiagnostics(fr);
			result.AddMappings(fr);
		}
		foreach (var sub in Directory.EnumerateDirectories(dir)) {
			LoadDirRecursive(sub, result);
		}
	}

	public async Task<LoadResult> LoadAsync(string content, string fallbackKey) {
		var result = new LoadResult();
		try { await Task.Run(() => ParseJson(content, fallbackKey, result)); }
		catch (JsonException ex) { result.AddDiagnostic($"Error parsing JSON for '{fallbackKey}': {ex.Message}"); }
		catch (IOException ex) { result.AddDiagnostic($"IO error for '{fallbackKey}': {ex.Message}"); }
		return result;
	}

	public async Task<LoadResult> LoadFileAsync(string path) {
		try {
			var content = await File.ReadAllTextAsync(path);
			return await LoadAsync(content, Path.GetFileNameWithoutExtension(path));
		}
		catch (JsonException ex) { var r = new LoadResult(); r.AddDiagnostic($"Error parsing '{path}': {ex.Message}"); return r; }
		catch (IOException ex) { var r = new LoadResult(); r.AddDiagnostic($"Error loading '{path}': {ex.Message}"); return r; }
	}

	public async Task<LoadResult> LoadDirectoryAsync(string path) {
		var result = new LoadResult();
		if (!Directory.Exists(path)) { result.AddDiagnostic($"Directory not found: {path}"); return result; }
		await LoadDirRecursiveAsync(path, result);
		return result;
	}

	private async Task LoadDirRecursiveAsync(string dir, LoadResult result) {
		foreach (var f in Directory.EnumerateFiles(dir, "*.json")) {
			var fr = await LoadFileAsync(f);
			result.AddBeings(fr);
			result.AddDiagnostics(fr);
			result.AddMappings(fr);
		}
		foreach (var sub in Directory.EnumerateDirectories(dir)) {
			await LoadDirRecursiveAsync(sub, result);
		}
	}

	private void ParseJson(string json, string fallbackKey, LoadResult result) {
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (root.ValueKind == JsonValueKind.Object) {
			foreach (var p in root.EnumerateObject()) {
				TryExtractBeing(p.Value, p.Name, fallbackKey, result, visited);
			}
		}
		else if (root.ValueKind == JsonValueKind.Array) {
			foreach (var item in root.EnumerateArray()) {
				TryExtractBeing(item, null, fallbackKey, result, visited);
			}
		}
	}

	private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase) {
		"$inherits", "inherits", "$key", "key"
	};

	private void TryExtractBeing(JsonElement obj, string? parentKey, string? fn, LoadResult result,
		HashSet<string> visited) {
		if (obj.ValueKind != JsonValueKind.Object) {
			return;
		}

		var key = ExtractKey(obj, parentKey, fn);
		if (key == null || !visited.Add(key)) {
			result.AddDiagnostic(key == null ? "Being has no key" : $"Duplicate being key '{key}' skipped");
			return;
		}

		var being = new RawBeing(key);

		if (obj.TryGetProperty("$inherits", out var inh) && inh.ValueKind == JsonValueKind.String) {
			being.Inherits = inh.GetString();
		}
		else if (obj.TryGetProperty("inherits", out var inh2) && inh2.ValueKind == JsonValueKind.String) {
			being.Inherits = inh2.GetString();
		}

		var conceptNames = _registry != null
			? new HashSet<string>(
				_registry.All.SelectMany(r => r.Concepts).Select(t => t.Name),
				StringComparer.OrdinalIgnoreCase)
			: null;

		foreach (var p in obj.EnumerateObject()) {
			if (ReservedNames.Contains(p.Name)) {
				continue;
			}

			if (p.Name.StartsWith('$')) {
				var conceptName = p.Name[1..];
				if (conceptNames == null || conceptNames.Contains(conceptName)) {
					being.Concepts.Add(conceptName);
					being.ConceptSet.Add(conceptName);

					if (p.Value.ValueKind == JsonValueKind.Object) {
						foreach (var asp in p.Value.EnumerateObject()) {
							var aspectName = asp.Name;
							being.FieldNames.Add(aspectName);
							being.RawFields[aspectName] = ToObject(asp.Value);

							result.AddMapping(conceptName, aspectName);
						}
					}
				}
				else {
					result.AddDiagnostic($"Unknown concept '{conceptName}' specified with '$' prefix in being '{key}'");
				}
			}
			else {
				var aspectName = p.Name;
				being.FieldNames.Add(aspectName);
				being.RawFields[aspectName] = ToObject(p.Value);
			}
		}

		result.AddBeing(being);
	}

	private static string? ExtractKey(JsonElement obj, string? parentKey, string? fn) {
		if (!string.IsNullOrEmpty(parentKey)) {
			return parentKey;
		}

		if (obj.TryGetProperty("$key", out var p) && p.ValueKind == JsonValueKind.String) {
			return p.GetString();
		}

		if (obj.TryGetProperty("key", out var p2) && p2.ValueKind == JsonValueKind.String) {
			return p2.GetString();
		}

		return fn;
	}

	private static object? ToObject(JsonElement el) => el.ValueKind switch {
		JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => ToObject(p.Value), StringComparer.OrdinalIgnoreCase),
		JsonValueKind.Array => el.EnumerateArray().Select(ToObject).ToList(),
		JsonValueKind.String => el.GetString(),
		JsonValueKind.Number => JsonToNumber(el),
		JsonValueKind.True => true,
		JsonValueKind.False => false,
		JsonValueKind.Null => null,
		JsonValueKind.Undefined => null,
		_ => null,
	};

	private static object JsonToNumber(JsonElement el) {
		if (el.TryGetInt32(out var i)) return i;
		if (el.TryGetInt64(out var l)) return l;
		return el.GetDouble();
	}
}
