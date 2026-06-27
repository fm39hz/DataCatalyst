namespace DataCatalyst.Pipeline.Stages;

using System;
using System.Collections.Generic;

public sealed class SchemaStage : IPipelineStage {
	public int Order => 30;

	public bool Execute(PipelineContext ctx) {
		var octx = (IOntologyContext)ctx;
		var lctx = (ILoadContext)ctx;
		var atr = octx.Registries.AspectTypes;
		var afr = octx.Registries.AspectFields;
		var br = octx.Registries.Beings;

		// Register all known aspect types with their fields
		foreach (var type in atr.RegisteredTypes) {
			var fields = afr.GetFields(type.Name) ?? [];
			octx.Schema.DefineAspect(type.Name, new Dictionary<string, Type>(fields));
		}

		// Register concepts from BeingRegistry + Mappings
		foreach (var record in br.All) {
			foreach (var conceptType in record.Concepts) {
				var cname = conceptType.Name;
				var aspects = lctx.Mappings.GetValueOrDefault(cname, []);
				octx.Schema.DefineConcept(cname, [.. aspects]);
			}
		}

		// Register remaining Mappings keys as standalone concepts
		foreach (var kv in lctx.Mappings) {
			if (!octx.Schema.GetConceptId(kv.Key).HasValue) {
				octx.Schema.DefineConcept(kv.Key, [.. kv.Value]);
			}
		}

		return true;
	}
}
