namespace DataCatalyst.Loaders;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataCatalyst.Core;

/// <summary>Result of a JSON directory load operation, containing resolved entries and diagnostics.</summary>
public sealed class LoadResult {
	/// <summary>The successfully loaded data entries.</summary>
	public List<DataEntry> Entries { get; } = [];

	/// <summary>Warnings, errors, or info messages collected during the load operation.</summary>
	public List<string> Diagnostics { get; } = [];
}

/// <summary>Loads data entries from JSON files.</summary>
public static class JsonDataLoader {
	/// <summary>Loads entries from all JSON files in a directory. NOT Native AOT/Trim compatible.</summary>
	[Obsolete("Use LoadDirectory with JsonSerializerOptions for Native AOT compatibility.")]
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reflection-based serialization is not Native AOT compatible. Use the overload accepting JsonSerializerOptions.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Reflection-based serialization is not Native AOT compatible. Use the overload accepting JsonSerializerOptions.")]
	public static List<DataEntry> LoadDirectory(string directory) {
		return LoadDirectoryReflection(directory).Entries;
	}

	/// <summary>Loads entries from all JSON files in a directory in an AOT-safe manner.</summary>
	public static LoadResult LoadDirectory(string directory, JsonSerializerOptions options) {
		if (options == null) {
			throw new ArgumentNullException(nameof(options), "JsonSerializerOptions must be provided for AOT compatibility.");
		}

		var result = new LoadResult();
		if (!Directory.Exists(directory)) {
			result.Diagnostics.Add($"Directory '{directory}' does not exist.");
			return result;
		}

		var knownPrimitives = PrimitiveRegistry.GetAll();
		var shortNames = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
		var fullNames = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
		var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var t in knownPrimitives) {
			var shortName = t.Name;
			var fullName = t.FullName ?? t.Name;

			fullNames[fullName] = t;

			if (!shortNames.TryAdd(shortName, t)) {
				duplicates.Add(shortName);
			}
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

					Type? type;
					if (prop.Name.Contains('.')) {
						fullNames.TryGetValue(prop.Name, out type);
					} else {
						if (duplicates.Contains(prop.Name)) {
							result.Diagnostics.Add($"Ambiguous component short name '{prop.Name}' in file '{file}'. Use fully-qualified name.");
							continue;
						}
						shortNames.TryGetValue(prop.Name, out type);
					}

					if (type == null) {
						result.Diagnostics.Add($"Unrecognized component type '{prop.Name}' in file '{file}'. Make sure it is marked with [DataComponent] and registered.");
						continue;
					}

					try {
						var typeInfo = options.GetTypeInfo(type);
						var deserialized = JsonSerializer.Deserialize(prop.Value.GetRawText(), typeInfo);
						if (deserialized != null) {
							components[type] = deserialized;
						} else {
							result.Diagnostics.Add($"Failed to deserialize component '{prop.Name}' in file '{file}': Result was null.");
						}
					}
					catch (Exception ex) {
						result.Diagnostics.Add($"Error deserializing component '{prop.Name}' in file '{file}': {ex.Message}");
					}
				}

				result.Entries.Add(new DataEntry(key, components, inherits) { SourceFile = file });
			}
			catch (Exception ex) {
				result.Diagnostics.Add($"Failed to load file '{file}': {ex.Message}");
			}
		}

		return result;
	}

	/// <summary>Loads entries using reflection. NOT Native AOT/Trim compatible.</summary>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reflection-based serialization is not Native AOT compatible. Use the overload accepting JsonSerializerOptions.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Reflection-based serialization is not Native AOT compatible. Use the overload accepting JsonSerializerOptions.")]
	public static LoadResult LoadDirectoryReflection(string directory) {
		var result = new LoadResult();
		if (!Directory.Exists(directory)) {
			result.Diagnostics.Add($"Directory '{directory}' does not exist.");
			return result;
		}

		var knownPrimitives = PrimitiveRegistry.GetAll();
		var shortNames = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
		var fullNames = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
		var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var t in knownPrimitives) {
			var shortName = t.Name;
			var fullName = t.FullName ?? t.Name;

			fullNames[fullName] = t;

			if (!shortNames.TryAdd(shortName, t)) {
				duplicates.Add(shortName);
			}
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

					Type? type;
					if (prop.Name.Contains('.')) {
						fullNames.TryGetValue(prop.Name, out type);
					} else {
						if (duplicates.Contains(prop.Name)) {
							result.Diagnostics.Add($"Ambiguous component short name '{prop.Name}' in file '{file}'. Use fully-qualified name.");
							continue;
						}
						shortNames.TryGetValue(prop.Name, out type);
					}

					if (type == null) {
						result.Diagnostics.Add($"Unrecognized component type '{prop.Name}' in file '{file}'. Make sure it is marked with [DataComponent] and registered.");
						continue;
					}

					try {
						var deserialized = JsonSerializer.Deserialize(prop.Value.GetRawText(), type);
						if (deserialized != null) {
							components[type] = deserialized;
						} else {
							result.Diagnostics.Add($"Failed to deserialize component '{prop.Name}' in file '{file}': Result was null.");
						}
					}
					catch (Exception ex) {
						result.Diagnostics.Add($"Error deserializing component '{prop.Name}' in file '{file}': {ex.Message}");
					}
				}

				result.Entries.Add(new DataEntry(key, components, inherits) { SourceFile = file });
			}
			catch (Exception ex) {
				result.Diagnostics.Add($"Failed to load file '{file}': {ex.Message}");
			}
		}

		return result;
	}

	private static string MakeKey(string rootDir, string filePath) {
		var fullDir = Path.GetFullPath(rootDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
			+ Path.DirectorySeparatorChar;
		var rel = filePath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase)
			? filePath[fullDir.Length..] : filePath;
		return Path.ChangeExtension(rel, null).Replace('\\', '.').Replace('/', '.');
	}
}
