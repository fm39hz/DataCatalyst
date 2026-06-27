namespace DataCatalyst.Pipeline.Stages;

using System.IO;
using System.Linq;
using System.Text.Json;
using DataCatalyst.Registry;

public sealed class OntologyStage : IPipelineStage {
	public int Order => 5;

	public bool Execute(PipelineContext ctx) {
		var octx = (IOntologyContext)ctx;
		var reg = octx.Registries.Requires;
		foreach (var path in octx.OntologyPaths) {
			if (!File.Exists(path)) {
				octx.Diagnostics.Warn($"Ontology file not found: {path}");
				continue;
			}

			octx.Diagnostics.Info($"Loading ontology: {path}");
			var json = File.ReadAllText(path);
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			RegisterGroups(root, "concepts", octx, reg);
			RegisterGroups(root, "relations", octx, reg);
		}

		return true;
	}

	private static void RegisterGroups(JsonElement root, string groupName, IOntologyContext octx, IRequiresRegistry reg) {
		if (!root.TryGetProperty(groupName, out var group) || group.ValueKind != JsonValueKind.Object) return;
		foreach (var entry in group.EnumerateObject()) {
			var name = entry.Name;
			var reveals = ExtractArray(entry.Value, "$reveals");
			var suggests = ExtractArray(entry.Value, "$suggests");
			octx.Schema.DefineConcept(name, reveals);
			reg.Register(name, reveals, suggests);
		}
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
