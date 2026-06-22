namespace DataCatalyst.Loaders;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DataCatalyst.Abstractions;
using DataCatalyst.Core;

public class JsonDataLoader : IDataLoader {
	private const string JsonFilter = "*.json";
	private readonly JsonSerializerOptions _options;
	private readonly DataCatalystEnvironment _env;

	/// <summary>Default options: camelCase, case-insensitive, AOT-safe.</summary>
	public static JsonSerializerOptions DefaultOptions => new() {
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		TypeInfoResolver = new DefaultJsonTypeInfoResolver()
	};

	/// <summary>Creates a loader with default camelCase settings.</summary>
	public JsonDataLoader() : this(DefaultOptions, null) { }

	public JsonDataLoader(JsonSerializerOptions options, DataCatalystEnvironment? env = null) {
		_options = options;
		_env = env ?? new DataCatalystEnvironment();
	}

	public LoadResult LoadFile(string path) {
		var result = new LoadResult();
		var key = Path.GetFileNameWithoutExtension(path);
		var text = File.ReadAllText(path);
		if (TryParseEntry(key, text, _options, _env, out var entry, out var diag)) {
			result._entries.Add(entry);
		}
		if (diag != null) result._diagnostics.AddRange(diag);

		foreach (var p in _env.Plugins.EnabledPlugins.OfType<IPostLoadPlugin>())
			p.OnEntriesLoaded(result._entries, result._diagnostics);

		return result;
	}

	public LoadResult LoadDirectory(string path) {
		return LoadDirectory(path, _options, _env);
	}

	// --- Static API (backward compat) ---

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

	private static Type? ResolveComponent(string name, PrimitiveRegistry primitives) => primitives.TryResolveId(name, out var type) ? type : null;

	private static bool IsConceptProperty(string name) =>
		string.Equals(name, "Concept", StringComparison.Ordinal) ||
		string.Equals(name, "concept", StringComparison.Ordinal);

	private static string? TryGetConceptValue(JsonElement root) {
		if (root.TryGetProperty("Concept", out var val) && val.ValueKind == JsonValueKind.String)
			return val.GetString();
		if (root.TryGetProperty("concept", out val) && val.ValueKind == JsonValueKind.String)
			return val.GetString();
		return null;
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

				var conceptName = TryGetConceptValue(root);
				if (conceptName != null) {
					components[typeof(Concept)] = new Concept { Value = new[] { conceptName } };
				}

				foreach (var prop in root.EnumerateObject()) {
					if (prop.Name == keyField) continue;
					if (IsConceptProperty(prop.Name)) continue;

					var compType = ResolveComponent(prop.Name, primitives);
					if (compType == null) {
						diagnostics ??= new List<string>();
						diagnostics.Add($"Unknown field '{prop.Name}' in entry '{key}'. Type mapping not found — value skipped.");
						continue;
					}

					object deserialized;
					if (prop.Value.ValueKind == JsonValueKind.Object) {
						deserialized = JsonSerializer.Deserialize(prop.Value.GetRawText(), compType, options)!;
					} else if (prop.Value.ValueKind == JsonValueKind.String) {
						var wrapped = $"{{\"Value\":[\"{prop.Value.GetString()}\"]}}";
						deserialized = JsonSerializer.Deserialize(wrapped, compType, options)!;
						if (deserialized == null) {
							wrapped = $"{{\"Value\":\"{prop.Value.GetString()}\"}}";
							deserialized = JsonSerializer.Deserialize(wrapped, compType, options)!;
						}
					} else {
						var wrapped = $"{{\"Value\":{prop.Value.GetRawText()}}}";
						deserialized = JsonSerializer.Deserialize(wrapped, compType, options)!;
					}
					if (deserialized != null) components[compType] = deserialized;
				}

				if (!components.ContainsKey(typeof(Concept))) {
					diagnostics ??= new List<string>();
					diagnostics.Add($"Entry '{key}' has no Concept field — assigned to 'Core' concept.");
					components[typeof(Concept)] = new Concept { Value = new[] { "Core" } };
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
