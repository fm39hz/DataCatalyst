namespace DataCatalyst.Loaders;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Core;

/// <summary>Result of a JSON directory load operation, containing resolved entries and diagnostics.</summary>
public sealed class LoadResult {
	/// <summary>The successfully loaded data entries.</summary>
	public List<DataEntry> Entries { get; } = [];

	/// <summary>Warnings, errors, or info messages collected during the load operation.</summary>
	public List<string> Diagnostics { get; } = [];
}

/// <summary>Loads data entries from JSON files using component discriminators registered in PrimitiveRegistry.</summary>
public static class JsonDataLoader {
	private static Type? ResolveComponent(string name, PrimitiveRegistry primitives) {
		return primitives.TryResolveId(name, out var type) ? type : null;
	}

	/// <summary>Loads entries from all JSON files in a directory in an AOT-safe manner.</summary>
	public static LoadResult LoadDirectory(string directory, JsonSerializerOptions options, DataCatalystEnvironment? env = null) {
		env ??= DataCatalystEnvironment.Default;
		if (options == null) {
			throw new ArgumentNullException(nameof(options),
				"JsonSerializerOptions must be provided for AOT compatibility.");
		}

		var result = new LoadResult();
		if (!Directory.Exists(directory)) {
			result.Diagnostics.Add($"Directory '{directory}' does not exist.");
			return result;
		}

		var files = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories);
		Array.Sort(files, StringComparer.OrdinalIgnoreCase);

		foreach (var file in files) {
			try {
				var text = File.ReadAllText(file);
				using var doc = JsonDocument.Parse(text);
				var root = doc.RootElement;

				var key = MakeKey(directory, file);

				List<string>? inherits = null;
				if (root.TryGetProperty("inherits", out var inhEl) && inhEl.ValueKind == JsonValueKind.Array) {
					inherits = [];
					foreach (var item in inhEl.EnumerateArray()) {
						inherits.Add(item.GetString() ?? "");
					}
				}

				var components = new Dictionary<Type, object>();
				foreach (var prop in root.EnumerateObject()) {
					if (prop.Name == "inherits") {
						continue;
					}

					if (prop.Value.ValueKind != JsonValueKind.Object) {
						continue;
					}

					var type = ResolveComponent(prop.Name, env.Primitives);
					if (type == null) {
						result.Diagnostics.Add(
							$"Unrecognized component discriminator '{prop.Name}' in file '{file}'. Make sure the type is marked with [DataComponent] and registered.");
						continue;
					}

					try {
						var typeInfo = options.GetTypeInfo(type);
						var deserialized = JsonSerializer.Deserialize(prop.Value.GetRawText(), typeInfo);
						if (deserialized != null) {
							components[type] = deserialized;
						}
						else {
							result.Diagnostics.Add(
								$"Failed to deserialize component '{prop.Name}' in file '{file}': Result was null.");
						}
					}
					catch (Exception ex) {
						result.Diagnostics.Add(
							$"Error deserializing component '{prop.Name}' in file '{file}': {ex.Message}");
					}
				}

				result.Entries.Add(new DataEntry(key, components, inherits) { SourceFile = file });
			}
			catch (Exception ex) {
				result.Diagnostics.Add($"Failed to load file '{file}': {ex.Message}");
			}
		}

		foreach (var p in env.Plugins.Plugins.OfType<IPostLoadPlugin>()) {
			p.OnEntriesLoaded(result.Entries, result.Diagnostics);
		}

		return result;
	}

	/// <summary>Loads entries from a single JSON file containing an array of objects in an AOT-safe manner.</summary>
	public static LoadResult LoadArray(string filePath, string keyField, JsonSerializerOptions options, DataCatalystEnvironment? env = null) {
		env ??= DataCatalystEnvironment.Default;
		if (options == null) {
			throw new ArgumentNullException(nameof(options),
				"JsonSerializerOptions must be provided for AOT compatibility.");
		}

		if (string.IsNullOrEmpty(keyField)) {
			throw new ArgumentException("Key field name must be provided.", nameof(keyField));
		}

		var result = new LoadResult();
		if (!File.Exists(filePath)) {
			result.Diagnostics.Add($"File '{filePath}' does not exist.");
			return result;
		}

		try {
			var text = File.ReadAllText(filePath);
			using var doc = JsonDocument.Parse(text);
			if (doc.RootElement.ValueKind != JsonValueKind.Array) {
				result.Diagnostics.Add($"Root of file '{filePath}' is not a JSON array.");
				return result;
			}

			var index = 0;
			foreach (var element in doc.RootElement.EnumerateArray()) {
				if (element.ValueKind != JsonValueKind.Object) {
					result.Diagnostics.Add($"Element at index {index} in file '{filePath}' is not a JSON object.");
					index++;
					continue;
				}

				if (!element.TryGetProperty(keyField, out var keyEl) || keyEl.ValueKind != JsonValueKind.String) {
					result.Diagnostics.Add($"Element at index {index} in file '{filePath}' is missing string key field '{keyField}'.");
					index++;
					continue;
				}

				var key = keyEl.GetString() ?? "";
				if (string.IsNullOrEmpty(key)) {
					result.Diagnostics.Add($"Element at index {index} in file '{filePath}' has empty key.");
					index++;
					continue;
				}

				List<string>? inherits = null;
				if (element.TryGetProperty("inherits", out var inhEl) && inhEl.ValueKind == JsonValueKind.Array) {
					inherits = [];
					foreach (var item in inhEl.EnumerateArray()) {
						inherits.Add(item.GetString() ?? "");
					}
				}

				var components = new Dictionary<Type, object>();
				foreach (var prop in element.EnumerateObject()) {
					if (prop.Name == "inherits" || prop.Name == keyField) {
						continue;
					}

					if (prop.Value.ValueKind != JsonValueKind.Object) {
						continue;
					}

					var type = ResolveComponent(prop.Name, env.Primitives);
					if (type == null) {
						result.Diagnostics.Add(
							$"Unrecognized component discriminator '{prop.Name}' in element '{key}'. Make sure the type is marked with [DataComponent] and registered.");
						continue;
					}

					try {
						var typeInfo = options.GetTypeInfo(type);
						var deserialized = JsonSerializer.Deserialize(prop.Value.GetRawText(), typeInfo);
						if (deserialized != null) {
							components[type] = deserialized;
						}
						else {
							result.Diagnostics.Add(
								$"Failed to deserialize component '{prop.Name}' in element '{key}': Result was null.");
						}
					}
					catch (Exception ex) {
						result.Diagnostics.Add(
							$"Error deserializing component '{prop.Name}' in element '{key}': {ex.Message}");
					}
				}

				result.Entries.Add(new DataEntry(key, components, inherits) { SourceFile = filePath });
				index++;
			}
		}
		catch (Exception ex) {
			result.Diagnostics.Add($"Failed to load and parse array file '{filePath}': {ex.Message}");
		}

		foreach (var p in env.Plugins.Plugins.OfType<IPostLoadPlugin>()) {
			p.OnEntriesLoaded(result.Entries, result.Diagnostics);
		}

		return result;
	}

	private static string MakeKey(string rootDir, string filePath) {
		var fullDir = Path.GetFullPath(rootDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
					+ Path.DirectorySeparatorChar;
		var rel = filePath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase)
			? filePath[fullDir.Length..]
			: filePath;
		return Path.ChangeExtension(rel, null).Replace('\\', '.').Replace('/', '.');
	}
}
