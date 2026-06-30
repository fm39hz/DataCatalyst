namespace DataCatalyst.Pipeline.Stages;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DataCatalyst.Registry;

public sealed class OntologyStage : IPipelineStage {
	public int Order => 5;

	public bool Execute(PipelineContext ctx) {
		var reg = ctx.Registries.Requires;
		var schema = ctx.Schema;
		var atr = ctx.Registries.AspectTypes;

		// 1. Process concept files — register concepts with $reveals/$suggests
		foreach (var path in ctx.AllConceptFiles) {
			if (!File.Exists(path)) continue;
			var json = File.ReadAllText(path);
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			if (!root.TryGetProperty("concepts", out var concepts) || concepts.ValueKind != JsonValueKind.Object)
				continue;

			foreach (var entry in concepts.EnumerateObject()) {
				var name = entry.Name;
				var reveals = ExtractArray(entry.Value, "$reveals");
				var suggests = ExtractArray(entry.Value, "$suggests");
				schema.DefineConcept(name, reveals);
				reg.Register(name, reveals, suggests);
			}
		}

		// 2. Process aspect files — register aspect types and IDs
		foreach (var path in ctx.AllAspectFiles) {
			if (!File.Exists(path)) continue;
			var json = File.ReadAllText(path);
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			if (!root.TryGetProperty("aspects", out var aspects) || aspects.ValueKind != JsonValueKind.Object)
				continue;

			foreach (var entry in aspects.EnumerateObject()) {
				var name = entry.Name;
				var def = entry.Value;
				// Register aspect type (key-only, no C# type available)
				if (def.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object) {
					var fieldMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
					foreach (var f in fields.EnumerateObject()) {
						fieldMap[f.Name] = typeof(object);
					}
					schema.DefineAspect(name, fieldMap);
				}
			}
		}

		return true;
	}

	private static string[] ExtractArray(JsonElement element, string key) {
		if (element.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array) {
			return arr.EnumerateArray()
				.Select(e => e.GetString())
				.Where(s => !string.IsNullOrEmpty(s))
				.ToArray()!;
		}
		return [];
	}
}
