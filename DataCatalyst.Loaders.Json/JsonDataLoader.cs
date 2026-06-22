namespace DataCatalyst.Loaders;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DataCatalyst.Abstractions;
using DataCatalyst.Core;

/// <summary>Result of a JSON directory load operation, containing resolved entries and diagnostics.</summary>
public sealed class LoadResult {
	internal readonly List<DataEntry> _entries = [];
	internal readonly List<string> _diagnostics = [];

	/// <summary>The successfully loaded data entries.</summary>
	public IReadOnlyList<DataEntry> Entries => _entries;

	/// <summary>Warnings, errors, or info messages collected during the load operation.</summary>
	public IReadOnlyList<string> Diagnostics => _diagnostics;
}

/// <summary>Loads data entries from JSON files using component discriminators registered in PrimitiveRegistry.</summary>
public static class JsonDataLoader {
	private const string JsonFilter = "*.json";

	private static Type? ResolveComponent(string name, PrimitiveRegistry primitives) => primitives.TryResolveId(name, out var type) ? type : null;

	/// <summary>Loads all JSON files from a directory.</summary>
	public static LoadResult LoadDirectory(string directory, JsonSerializerOptions options, DataCatalystEnvironment? env = null) {
		env ??= DataCatalystEnvironment.Default;
		var result = new LoadResult();

		foreach (var filePath in Directory.EnumerateFiles(directory, JsonFilter)) {
			var key = Path.GetFileNameWithoutExtension(filePath);
			var text = File.ReadAllText(filePath);

			if (TryParseEntry(key, text, options, env, out var entry, out var diag)) {
				result._entries.Add(entry);
			}
			if (diag != null) result._diagnostics.AddRange(diag);
		}

		foreach (var p in env.Plugins.EnabledPlugins.OfType<IPostLoadPlugin>()) {
			p.OnEntriesLoaded(result._entries, result._diagnostics);
		}

		return result;
	}

	/// <summary>Loads entries from a JSON array file.</summary>
	public static LoadResult LoadArray(string filePath, string keyField, JsonSerializerOptions options, DataCatalystEnvironment? env = null) {
		env ??= DataCatalystEnvironment.Default;
		var result = new LoadResult();
		var text = File.ReadAllText(filePath);

		using var doc = JsonDocument.Parse(text);
		if (doc.RootElement.ValueKind != JsonValueKind.Array) {
			result._diagnostics.Add($"Root element in '{filePath}' is not an array.");
			return result;
		}

		int index = 0;
		foreach (var element in doc.RootElement.EnumerateArray()) {
			if (!element.TryGetProperty(keyField, out var keyEl) || keyEl.ValueKind != JsonValueKind.String) {
				result._diagnostics.Add($"Element at index {index} in file '{filePath}' is missing string key field '{keyField}'.");
				index++;
				continue;
			}

			var key = keyEl.GetString();
			if (string.IsNullOrEmpty(key)) {
				result._diagnostics.Add($"Element at index {index} in file '{filePath}' has empty key field '{keyField}'.");
				index++;
				continue;
			}

			if (TryParseEntry(key, element.GetRawText(), options, env, out var entry, out var diag)) {
				entry.SourceFile = filePath;
				result._entries.Add(entry);
			}
			if (diag != null) result._diagnostics.AddRange(diag);
			index++;
		}

		return result;
	}

	private static bool TryParseEntry(string key, string json, JsonSerializerOptions options,
		DataCatalystEnvironment env, out DataEntry entry, out List<string>? diagnostics) {

		entry = null!;
		diagnostics = null;

		try {
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			if (root.ValueKind != JsonValueKind.Object) return false;

			var primitives = env.Primitives;
			var components = new Dictionary<Type, object>();
			var meta = new Dictionary<string, object>();
			var sourceFile = key;

			foreach (var prop in root.EnumerateObject()) {
				// Reserved meta fields (check before value kind filter)
				if (string.Equals(prop.Name, "inherits", StringComparison.OrdinalIgnoreCase)) {
					if (prop.Value.ValueKind == JsonValueKind.Array) {
						var list = new List<string>();
						foreach (var el in prop.Value.EnumerateArray()) {
							if (el.ValueKind == JsonValueKind.String) list.Add(el.GetString()!);
						}
						meta["inherits"] = list.ToArray();
					}
					continue;
				}

				if (string.Equals(prop.Name, "Concept", StringComparison.Ordinal)) {
					if (prop.Value.ValueKind == JsonValueKind.String) {
						meta["Concept"] = prop.Value.GetString()!;
					}
					continue;
				}

				if (prop.Value.ValueKind != JsonValueKind.Object) continue;

				// Component — resolve type
				var compType = ResolveComponent(prop.Name, primitives);
				if (compType == null) continue;

				var raw = prop.Value.GetRawText();
				var deserialized = JsonSerializer.Deserialize(raw, compType, options);
				if (deserialized != null) {
					components[compType] = deserialized;
				}
			}

			entry = new DataEntry(key, components, meta);
			return true;
		}
		catch (Exception ex) {
			diagnostics = [ex.Message];
			return false;
		}
	}
}
