namespace DataCatalyst.Loaders;
#pragma warning disable RS1035
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DataCatalyst.Storage;
using LoaderAbstractions = DataCatalyst.Loader;

public sealed class JsonDataLoader : LoaderAbstractions.IDataLoader {
	public LoaderAbstractions.LoadResult Load(string content, string fallbackKey) {
		var result = new LoaderAbstractions.LoadResult();
		try { ParseJson(content, fallbackKey, result); }
		catch (Exception ex) { result._diagnostics.Add($"Error parsing JSON for '{fallbackKey}': {ex.Message}"); }
		return result;
	}

	public LoaderAbstractions.LoadResult LoadFile(string path) {
		try { return Load(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path)); }
		catch (Exception ex) { var r = new LoaderAbstractions.LoadResult(); r._diagnostics.Add($"Error loading '{path}': {ex.Message}"); return r; }
	}

	public LoaderAbstractions.LoadResult LoadDirectory(string path) {
		var result = new LoaderAbstractions.LoadResult();
		if (!Directory.Exists(path)) { result._diagnostics.Add($"Directory not found: {path}"); return result; }
		foreach (var file in Directory.EnumerateFiles(path, "*.json")) {
			var fr = LoadFile(file);
			result._entries.AddRange(fr._entries);
			result._diagnostics.AddRange(fr._diagnostics);
			foreach (var kv in fr.Mappings) {
				if (!result._mappings.TryGetValue(kv.Key, out var list)) {
					result._mappings[kv.Key] = list = [];
				}

				foreach (var val in kv.Value) {
					if (!list.Contains(val)) {
						list.Add(val);
					}
				}
			}
		}
		return result;
	}

	private static void ParseJson(string json, string fallbackKey, LoaderAbstractions.LoadResult result) {
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (root.ValueKind == JsonValueKind.Object) {
			foreach (var p in root.EnumerateObject()) {
				TryExtractEntry(p.Value, p.Name, fallbackKey, result, visited);
			}
		}
		else if (root.ValueKind == JsonValueKind.Array) {
			foreach (var item in root.EnumerateArray()) {
				TryExtractEntry(item, null, fallbackKey, result, visited);
			}
		}
	}

	private static bool TryExtractEntry(JsonElement obj, string? parentKey, string? fn, LoaderAbstractions.LoadResult result,
		HashSet<string> visited) {
		if (obj.ValueKind != JsonValueKind.Object) {
			return false;
		}

		var key = ExtractKey(obj, parentKey, fn);
		if (key == null || !visited.Add(key)) {
			result._diagnostics.Add(key == null ? "Entry has no key" : $"Duplicate entry key '{key}' skipped");
			return true;
		}

		var entry = new RawEntry { Key = key };

		if (obj.TryGetProperty("$inherits", out var inh) && inh.ValueKind == JsonValueKind.String) {
			entry.Inherits = inh.GetString();
		}
		else if (obj.TryGetProperty("inherits", out var inh2) && inh2.ValueKind == JsonValueKind.String) {
			entry.Inherits = inh2.GetString();
		}

		var conceptNames = new HashSet<string>(
			Registry.EntryRegistry.All.SelectMany(r => r.Concepts).Select(t => t.Name),
			StringComparer.OrdinalIgnoreCase
		);

		foreach (var p in obj.EnumerateObject()) {
			if (p.Name.Equals("$inherits", StringComparison.OrdinalIgnoreCase) ||
				p.Name.Equals("inherits", StringComparison.OrdinalIgnoreCase) ||
				p.Name.Equals("$key", StringComparison.OrdinalIgnoreCase) ||
				p.Name.Equals("key", StringComparison.OrdinalIgnoreCase)) {
				continue;
			}

			// Check if it starts with '$' (Concepts)
			if (p.Name.StartsWith('$')) {
				var conceptName = p.Name[1..];
				if (conceptNames.Contains(conceptName)) {
					entry.Concepts.Add(conceptName);
					entry.ConceptSet.Add(conceptName);

					if (p.Value.ValueKind == JsonValueKind.Object) {
						foreach (var asp in p.Value.EnumerateObject()) {
							var aspectName = asp.Name;
							entry.FieldNames.Add(aspectName);
							entry.RawFields[aspectName] = ToObject(asp.Value);

							if (!result._mappings.TryGetValue(conceptName, out var list)) {
								result._mappings[conceptName] = list = [];
							}

							if (!list.Contains(aspectName)) {
								list.Add(aspectName);
							}
						}
					}
				}
				else {
					result._diagnostics.Add($"Unknown concept '{conceptName}' specified with '$' prefix in entry '{key}'");
				}
			}
			else {
				// Entry-level aspect (like Stamina)
				var aspectName = p.Name;
				entry.FieldNames.Add(aspectName);
				entry.RawFields[aspectName] = ToObject(p.Value);
			}
		}

		result._entries.Add(entry);
		return true;
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
		JsonValueKind.Number => el.TryGetInt32(out var i) ? i : el.TryGetInt64(out var l) ? l : el.GetDouble(),
		JsonValueKind.True => true,
		JsonValueKind.False => false,
		JsonValueKind.Undefined => throw new NotImplementedException(),
		JsonValueKind.Null => throw new NotImplementedException(),
		_ => null,
	};
}
