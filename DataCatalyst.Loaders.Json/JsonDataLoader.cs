namespace DataCatalyst.Loaders;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DataCatalyst.Abstractions;
using DataCatalyst.Core;

public sealed class LoadResult {
	internal readonly List<DataEntry> _entries = [];
	internal readonly List<string> _diagnostics = [];

	public IReadOnlyList<DataEntry> Entries => _entries;
	public IReadOnlyList<string> Diagnostics => _diagnostics;
}

public static class JsonDataLoader {
	private const string JsonFilter = "*.json";
	private static Type? ResolveComponent(string name, PrimitiveRegistry primitives) => primitives.TryResolveId(name, out var type) ? type : null;

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

		foreach (var p in env.Plugins.EnabledPlugins.OfType<IPostLoadPlugin>())
			p.OnEntriesLoaded(result._entries, result._diagnostics);

		return result;
	}

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
				index++; continue;
			}
			var key = keyEl.GetString();
			if (string.IsNullOrEmpty(key)) {
				result._diagnostics.Add($"Element at index {index} in file '{filePath}' has empty key field '{keyField}'.");
				index++; continue;
			}
			if (TryParseEntry(key, element.GetRawText(), options, env, out var entry, out var diag, keyField)) {
				result._entries.Add(entry);
			}
			if (diag != null) result._diagnostics.AddRange(diag);
			index++;
		}
		return result;
	}

	private static bool TryParseEntry(string key, string json, JsonSerializerOptions options,
		DataCatalystEnvironment env, out DataEntry entry, out List<string>? diagnostics,
		string? keyField = null) {

		entry = null!;
		diagnostics = null;
		try {
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			if (root.ValueKind != JsonValueKind.Object) return false;

				var primitives = env.Primitives;
				var components = new Dictionary<Type, object>();

				foreach (var prop in root.EnumerateObject()) {
					if (prop.Name == keyField) continue;

					var compType = ResolveComponent(prop.Name, primitives);
					if (compType == null) {
						diagnostics ??= new List<string>();
						diagnostics.Add($"Unknown field '{prop.Name}' in entry '{key}'. Type mapping not found — value skipped.");
						continue;
					}

					object deserialized;
					if (prop.Value.ValueKind == JsonValueKind.Object) {
						deserialized = JsonSerializer.Deserialize(prop.Value.GetRawText(), compType, options)!;
					} else {
						var wrapped = $"{{\"Value\":{prop.Value.GetRawText()}}}";
						deserialized = JsonSerializer.Deserialize(wrapped, compType, options)!;
					}
					if (deserialized != null) components[compType] = deserialized;
				}

				entry = new DataEntry(key, components);
			return true;
		}
		catch (Exception ex) {
			diagnostics = [ex.Message];
			return false;
		}
	}
}
