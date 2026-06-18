namespace DataCatalyst.Loaders;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Core;

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
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Reflection-based serialization is not Native AOT compatible. Use the overload accepting JsonSerializerOptions.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(
		"Reflection-based serialization is not Native AOT compatible. Use the overload accepting JsonSerializerOptions.")]
	public static List<DataEntry> LoadDirectory(string directory) => LoadDirectoryReflection(directory).Entries;

	/// <summary>Loads entries from all JSON files in a directory in an AOT-safe manner.</summary>
	public static LoadResult LoadDirectory(string directory, JsonSerializerOptions options) {
		var registry = new DataRegistry();
		foreach (var t in PrimitiveRegistry.GetAll()) {
			registry.RegisterComponent(t);
		}

		return LoadDirectory(directory, registry, options);
	}

	/// <summary>Loads entries from all JSON files in a directory using a custom registry in an AOT-safe manner.</summary>
	public static LoadResult LoadDirectory(string directory, DataRegistry registry, JsonSerializerOptions options) {
		if (registry == null) {
			throw new ArgumentNullException(nameof(registry));
		}

		if (options == null) {
			throw new ArgumentNullException(nameof(options),
				"JsonSerializerOptions must be provided for AOT compatibility.");
		}

		var result = new LoadResult();
		if (!Directory.Exists(directory)) {
			result.Diagnostics.Add($"Directory '{directory}' does not exist.");
			return result;
		}

		var knownPrimitives = registry.GetComponents();
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
					}
					else {
						if (duplicates.Contains(prop.Name)) {
							result.Diagnostics.Add(
								$"Ambiguous component short name '{prop.Name}' in file '{file}'. Use fully-qualified name.");
							continue;
						}

						shortNames.TryGetValue(prop.Name, out type);
					}

					if (type == null) {
						result.Diagnostics.Add(
							$"Unrecognized component type '{prop.Name}' in file '{file}'. Make sure it is marked with [DataComponent] and registered.");
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

		return result;
	}

	/// <summary>Loads entries using reflection. NOT Native AOT/Trim compatible.</summary>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Reflection-based serialization is not Native AOT compatible. Use the overload accepting JsonSerializerOptions.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(
		"Reflection-based serialization is not Native AOT compatible. Use the overload accepting JsonSerializerOptions.")]
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
					}
					else {
						if (duplicates.Contains(prop.Name)) {
							result.Diagnostics.Add(
								$"Ambiguous component short name '{prop.Name}' in file '{file}'. Use fully-qualified name.");
							continue;
						}

						shortNames.TryGetValue(prop.Name, out type);
					}

					if (type == null) {
						result.Diagnostics.Add(
							$"Unrecognized component type '{prop.Name}' in file '{file}'. Make sure it is marked with [DataComponent] and registered.");
						continue;
					}

					try {
						var deserialized = JsonSerializer.Deserialize(prop.Value.GetRawText(), type);
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

		return result;
	}

	/// <summary>Loads entries from a single JSON file containing an array of objects in an AOT-safe manner.</summary>
	public static LoadResult LoadArray(string filePath, string keyField, JsonSerializerOptions options) {
		var registry = new DataRegistry();
		foreach (var t in PrimitiveRegistry.GetAll()) {
			registry.RegisterComponent(t);
		}

		return LoadArray(filePath, keyField, registry, options);
	}

	/// <summary>Loads entries from a single JSON file containing an array of objects using a custom registry in an AOT-safe manner.</summary>
	public static LoadResult LoadArray(string filePath, string keyField, DataRegistry registry, JsonSerializerOptions options) {
		if (registry == null) {
			throw new ArgumentNullException(nameof(registry));
		}

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

		var knownPrimitives = registry.GetComponents();
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

					Type? type;
					if (prop.Name.Contains('.')) {
						fullNames.TryGetValue(prop.Name, out type);
					}
					else {
						if (duplicates.Contains(prop.Name)) {
							result.Diagnostics.Add(
								$"Ambiguous component short name '{prop.Name}' in element '{key}'. Use fully-qualified name.");
							continue;
						}

						shortNames.TryGetValue(prop.Name, out type);
					}

					if (type == null) {
						result.Diagnostics.Add(
							$"Unrecognized component type '{prop.Name}' in element '{key}'. Make sure it is marked with [DataComponent] and registered.");
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

		return result;
	}

	/// <summary>Loads entries from a single JSON file containing an array of objects. NOT Native AOT/Trim compatible.</summary>
	[Obsolete("Use LoadArray with JsonSerializerOptions for Native AOT compatibility.")]
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Reflection-based serialization is not Native AOT compatible. Use the overload accepting JsonSerializerOptions.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(
		"Reflection-based serialization is not Native AOT compatible. Use the overload accepting JsonSerializerOptions.")]
	public static LoadResult LoadArrayReflection(string filePath, string keyField) {
		if (string.IsNullOrEmpty(keyField)) {
			throw new ArgumentException("Key field name must be provided.", nameof(keyField));
		}

		var result = new LoadResult();
		if (!File.Exists(filePath)) {
			result.Diagnostics.Add($"File '{filePath}' does not exist.");
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

					Type? type;
					if (prop.Name.Contains('.')) {
						fullNames.TryGetValue(prop.Name, out type);
					}
					else {
						if (duplicates.Contains(prop.Name)) {
							result.Diagnostics.Add(
								$"Ambiguous component short name '{prop.Name}' in element '{key}'. Use fully-qualified name.");
							continue;
						}

						shortNames.TryGetValue(prop.Name, out type);
					}

					if (type == null) {
						result.Diagnostics.Add(
							$"Unrecognized component type '{prop.Name}' in element '{key}'. Make sure it is marked with [DataComponent] and registered.");
						continue;
					}

					try {
						var deserialized = JsonSerializer.Deserialize(prop.Value.GetRawText(), type);
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
