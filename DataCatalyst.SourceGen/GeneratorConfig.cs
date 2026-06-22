namespace DataCatalyst;

using System.Collections.Generic;
using System.Text.Json;

/// <summary>Config from datacatalyst.json — controls generated code customization.</summary>
public sealed class GeneratorConfig {
	public string Namespace { get; set; } = "DataCatalyst.Generated";
	public List<string> Usings { get; set; } = [];
	public Dictionary<string, List<string>> Attributes { get; set; } = [];

	public static GeneratorConfig? Load(string json) {
		try {
			var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			var cfg = new GeneratorConfig();

			if (root.TryGetProperty("namespace", out var ns) && ns.ValueKind == JsonValueKind.String)
				cfg.Namespace = ns.GetString()!;

			if (root.TryGetProperty("usings", out var us) && us.ValueKind == JsonValueKind.Array)
				foreach (var u in us.EnumerateArray())
					if (u.ValueKind == JsonValueKind.String)
						cfg.Usings.Add(u.GetString()!);

			if (root.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object) {
				foreach (var prop in attrs.EnumerateObject()) {
					if (prop.Value.ValueKind == JsonValueKind.Array) {
						var list = new List<string>();
						foreach (var v in prop.Value.EnumerateArray())
							if (v.ValueKind == JsonValueKind.String)
								list.Add(v.GetString()!);
						cfg.Attributes[prop.Name] = list;
					}
				}
			}

			return cfg;
		}
		catch {
			return null;
		}
	}
}
