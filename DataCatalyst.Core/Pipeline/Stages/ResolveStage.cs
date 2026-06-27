namespace DataCatalyst.Pipeline.Stages;

using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Storage;

public sealed class ResolveStage : IPipelineStage {
	public int Order => 60;

	public bool Execute(PipelineContext ctx) {
		var rctx = (IResolveContext)ctx;
		if (rctx.Raw == null) {
			return true;
		}

		var resolved = new List<ResolvedBeing>();

		foreach (var raw in rctx.Raw) {
			var rb = new ResolvedBeing(raw.Key);
			rb.AssignedIndex = raw.AssignedIndex;
			rb.Inherits = raw.Inherits;

			foreach (var kv in raw.Components)
				rb.Components[kv.Key] = kv.Value;

			foreach (var cn in raw.Concepts) {
				var cid = rctx.Schema.GetConceptId(cn);
				if (cid.HasValue) {
					rb.Concepts.Add(cid.Value);
					rb.ConceptSet.Add(cid.Value);
				}
			}

			foreach (var kv in raw.RawFields) {
				var aid = rctx.Schema.GetAspectId(kv.Key);
				if (aid.HasValue)
					rb.AspectFields[aid.Value] = kv.Value;
			}

			resolved.Add(rb);
		}

		rctx.Resolved = resolved;
		rctx.Raw = null;
		return !rctx.Diagnostics.HasErrors;
	}
}
