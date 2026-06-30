namespace Catalyst.Pipeline.Stages;

using System;
using System.Collections.Generic;

public sealed class SchemaStage : IPipelineStage {
	public int Order => 30;

	public bool Execute(PipelineContext ctx) {
		var schema = ctx.Schema;
		var atr = ctx.Registries.AspectTypes;
		var afr = ctx.Registries.AspectFields;
		var br = ctx.Registries.Beings;

		foreach (var type in atr.RegisteredTypes) {
			var fields = afr.GetFields(type.Name) ?? [];
			schema.DefineAspect(type.Name, new Dictionary<string, Type>(fields));
		}

		foreach (var record in br.All) {
			foreach (var conceptType in record.Concepts) {
				var cname = conceptType.Name;
				var aspects = ctx.Mappings.GetValueOrDefault(cname, []);
				schema.DefineConcept(cname, [.. aspects]);
			}
		}

		foreach (var kv in ctx.Mappings) {
			if (!schema.GetConceptId(kv.Key).HasValue) {
				schema.DefineConcept(kv.Key, [.. kv.Value]);
			}
		}

		return true;
	}
}
