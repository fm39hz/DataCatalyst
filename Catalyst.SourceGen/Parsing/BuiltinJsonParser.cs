namespace Catalyst.SourceGen.Parsing;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Catalyst.SourceGen.Models;

internal sealed class BuiltinJsonParser : IOntologyParser {
	public bool CanHandle(in OntologyFile file) {
		var ext = Path.GetExtension(file.FileName);
		if (!ext.Equals(".json", StringComparison.OrdinalIgnoreCase)) {
			return false;
		}

		try {
			using var doc = JsonDocument.Parse(file.Content);
			var root = doc.RootElement;
			if (root.ValueKind != JsonValueKind.Object) {
				return false;
			}

			if (root.TryGetProperty("concepts", out var c) && c.ValueKind == JsonValueKind.Object) {
				return true;
			}

			if (root.TryGetProperty("aspects", out var a) && a.ValueKind == JsonValueKind.Object) {
				return true;
			}

			if (root.TryGetProperty("relations", out var r) && r.ValueKind == JsonValueKind.Object) {
				return true;
			}

			return false;
		}
		catch (JsonException) { return false; }
	}

	public void Parse(in OntologyFile file, OntologyBuilder builder) {
		try {
			using var doc = JsonDocument.Parse(file.Content);
			var root = doc.RootElement;
			if (root.TryGetProperty("aspects", out var aspRoot) && aspRoot.ValueKind == JsonValueKind.Object) {
				foreach (var aspEntry in aspRoot.EnumerateObject()) {
					var aDef = aspEntry.Value;
					if (aDef.ValueKind != JsonValueKind.Object) {
						continue;
					}

					if (!aDef.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object) {
						continue;
					}

					var fieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					foreach (var field in fields.EnumerateObject()) {
						if (field.Value.ValueKind == JsonValueKind.String) {
							fieldMap[field.Name] = field.Value.GetString()!;
						}
					}
					if (fieldMap.Count > 0 && !builder.AspectFields.ContainsKey(aspEntry.Name)) {
						builder.AddAspectFields(aspEntry.Name, fieldMap);
					}
				}
			}
			if (!root.TryGetProperty("concepts", out var concepts) || concepts.ValueKind != JsonValueKind.Object) {
				return;
			}

			foreach (var conceptEntry in concepts.EnumerateObject()) {
				var cName = conceptEntry.Name;
				var entry = conceptEntry.Value;
				if (entry.ValueKind != JsonValueKind.Object) {
					continue;
				}

				if (!entry.TryGetProperty("$reveals", out var reqEl)) {
					entry.TryGetProperty("$requires", out reqEl);
				}

				if (reqEl.ValueKind == JsonValueKind.Array) {
					var list = new List<string>();
					foreach (var item in reqEl.EnumerateArray()) {
						if (item.ValueKind == JsonValueKind.String) {
							list.Add(item.GetString()!);
						}
					}
					if (list.Count > 0 && !builder.Requires.ContainsKey(cName)) {
						builder.AddRequires(cName, [.. list]);
					}
				}
				if (entry.TryGetProperty("$suggests", out var sug) && sug.ValueKind == JsonValueKind.Array) {
					var list = new List<string>();
					foreach (var item in sug.EnumerateArray()) {
						if (item.ValueKind == JsonValueKind.String) {
							list.Add(item.GetString()!);
						}
					}

					if (list.Count > 0 && !builder.Suggests.ContainsKey(cName)) {
						builder.AddSuggests(cName, [.. list]);
					}
				}
				if (!builder.Requires.ContainsKey(cName) && !builder.Suggests.ContainsKey(cName)) {
					builder.AddRequires(cName);
				}
			}
		}
		catch (JsonException) { }
	}
}
