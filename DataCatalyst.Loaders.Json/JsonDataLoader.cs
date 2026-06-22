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
			if (TryParseEntry(key, element.GetRawText(), options, env, out var entry, out var diag)) {
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
			var schema = env.Schema;
			var components = new Dictionary<Type, object>();
			var fields = new Dictionary<Type, object>();

			foreach (var prop in root.EnumerateObject()) {
				var resolvedType = schema.ResolveType(prop.Name);
				if (resolvedType != null) {
					if (resolvedType == typeof(string[])) {
						if (prop.Value.ValueKind == JsonValueKind.Array) {
							var list = new List<string>();
							foreach (var el in prop.Value.EnumerateArray())
								if (el.ValueKind == JsonValueKind.String) list.Add(el.GetString()!);
							fields[typeof(string[])] = list.ToArray();
						}
					}
					else if (resolvedType == typeof(int)) {
						if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var intVal))
							fields[typeof(int)] = intVal;
					}
					else if (resolvedType == typeof(string)) {
						if (prop.Value.ValueKind == JsonValueKind.String)
							fields[typeof(string)] = prop.Value.GetString()!;
					}
					continue;
				}

				if (prop.Value.ValueKind == JsonValueKind.Object) {
					var compType = ResolveComponent(prop.Name, primitives);
					if (compType == null) continue;
					var raw = prop.Value.GetRawText();
					var deserialized = JsonSerializer.Deserialize(raw, compType, options);
					if (deserialized != null) components[compType] = deserialized;
					continue;
				}

				var inferredType = InferFieldType(prop.Value);
				if (inferredType != null)
					fields[inferredType.Value.Type] = inferredType.Value.Value;
			}

			entry = new DataEntry(key, components, fields);
			return true;
		}
		catch (Exception ex) {
			diagnostics = [ex.Message];
			return false;
		}
	}

	private static (Type Type, object Value)? InferFieldType(JsonElement value) {
		switch (value.ValueKind) {
			case JsonValueKind.String:
				return (typeof(string), value.GetString()!);
			case JsonValueKind.Number:
				if (value.TryGetInt32(out var intVal)) return (typeof(int), intVal);
				if (value.TryGetInt64(out var longVal)) return (typeof(long), longVal);
				return (typeof(double), value.GetDouble());
			case JsonValueKind.True:
			case JsonValueKind.False:
				return (typeof(bool), value.GetBoolean());
			case JsonValueKind.Array:
				if (value.GetArrayLength() == 0) return null;
				var first = value[0];
				if (first.ValueKind == JsonValueKind.String) {
					var list = new List<string>();
					foreach (var el in value.EnumerateArray())
						if (el.ValueKind == JsonValueKind.String) list.Add(el.GetString()!);
					return (typeof(string[]), list.ToArray());
				}
				if (first.ValueKind == JsonValueKind.Number) {
					if (first.TryGetInt32(out _)) {
						var list = new List<int>();
						foreach (var el in value.EnumerateArray())
							if (el.TryGetInt32(out var n)) list.Add(n);
						return (typeof(int[]), list.ToArray());
					}
				}
				return null;
			default:
				return null;
		}
	}
}
